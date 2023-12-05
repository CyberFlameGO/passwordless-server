using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Passwordless.Common.Utils;
using Passwordless.Service.Helpers;
using Passwordless.Service.Models;
using Passwordless.Service.Storage.Ef;

namespace Passwordless.Service;

public record AppDeletionResult(string Message, bool IsDeleted, DateTime? DeleteAt, IReadOnlyCollection<string> AdminEmails);

public interface ISharedManagementService
{
    Task<bool> IsAvailable(string appId);
    Task<AccountKeysCreation> GenerateAccount(string appId, AppCreateDTO appCreationOptions);
    Task<string> ValidateSecretKey(string secretKey);
    Task<string> ValidatePublicKey(string publicKey);
    Task FreezeAccount(string accountName);
    Task UnFreezeAccount(string accountName);
    Task<AppDeletionResult> DeleteApplicationAsync(string appId);
    Task<AppDeletionResult> MarkDeleteApplicationAsync(string appId, string deletedBy, string baseUrl);
    Task<IEnumerable<string>> GetApplicationsPendingDeletionAsync();
    Task SetFeaturesAsync(string appId, ManageFeaturesDto payload);
    Task<AppFeatureDto> GetFeaturesAsync(string appId);
}

public class SharedManagementService : ISharedManagementService
{
    private readonly ILogger _logger;
    private readonly IConfiguration config;
    private readonly ISystemClock _systemClock;
    private readonly ITenantStorageFactory tenantFactory;
    private readonly IGlobalStorageFactory _globalStorageFactory;

    public SharedManagementService(ITenantStorageFactory tenantFactory,
        IGlobalStorageFactory globalStorageFactory,
        IConfiguration config,
        ISystemClock systemClock,
        ILogger<SharedManagementService> logger)
    {
        this.tenantFactory = tenantFactory;
        _globalStorageFactory = globalStorageFactory;
        this.config = config;
        _systemClock = systemClock;
        _logger = logger;
    }


    public async Task<bool> IsAvailable(string accountName)
    {
        // check if tenant already exists
        var storage = tenantFactory.Create(accountName);
        return !await storage.TenantExists();
    }

    public async Task<AccountKeysCreation> GenerateAccount(string appId, AppCreateDTO appCreationOptions)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ApiException($"'{nameof(appId)}' cannot be null, empty or whitespace.", 400);
        }
        if (appCreationOptions == null)
        {
            throw new ApiException("No application creation options have been defined.", 400);
        }

        var accountName = appId;
        var adminEmail = appCreationOptions.AdminEmail;

        if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(accountName))
        {
            throw new ApiException("Please set argument 'accountName' and 'adminEmail'", 400);
        }

        var regi = new Regex("^[A-Za-z][A-Za-z0-9]{2,62}$");
        var isValid = regi.IsMatch(accountName);

        if (!isValid)
        {
            throw new ApiException("accountName needs to be alphanumeric and start with a letter", 400);
        }

        ITenantStorage storage = tenantFactory.Create(accountName);

        if (await storage.TenantExists())
        {
            throw new ApiException($"accountName '{accountName}' is not available", 409);
        }

        string apiKey1 = await SetupApiKey(accountName, storage);
        string apiKey2 = await SetupApiKey(accountName, storage);

        (string original, string hashed) apiSecret1 = await SetupApiSecret(accountName, storage);
        (string original, string hashed) apiSecret2 = await SetupApiSecret(accountName, storage);

        var account = new AccountMetaInformation
        {
            AccountName = accountName,
            AdminEmails = new[] { adminEmail },
            CreatedAt = DateTime.UtcNow,
            SubscriptionTier = "Free",
            Features = new AppFeature
            {
                Tenant = accountName,
                EventLoggingIsEnabled = appCreationOptions.EventLoggingIsEnabled,
                EventLoggingRetentionPeriod = appCreationOptions.EventLoggingRetentionPeriod
            }
        };
        await storage.SaveAccountInformation(account);
        return new AccountKeysCreation
        {
            ApiKey1 = apiKey1,
            ApiKey2 = apiKey2,
            ApiSecret1 = apiSecret1.original,
            ApiSecret2 = apiSecret2.original,
            Message = "Store keys safely. They will only be shown to you once."
        };
    }

    public async Task<string> ValidateSecretKey(string secretKey)
    {
        var appId = GetAppId(secretKey);
        var storage = tenantFactory.Create(appId);

        var existingKey = await storage.GetApiKeyAsync(secretKey);
        if (existingKey != null)
        {
            existingKey.CheckLocked();

            if (ApiKeyUtils.Validate(existingKey.ApiKey, secretKey))
            {
                return appId;
            }
        }

        _logger.LogInformation("ApiSecret was not valid. {AppId} {ApiKey}", appId, secretKey?[..20]);
        throw new ApiException("ApiSecret was not valid", 401);
    }

    public async Task<string> ValidatePublicKey(string publicKey)
    {
        var appId = GetAppId(publicKey);
        var storage = tenantFactory.Create(appId);

        var existingKey = await storage.GetApiKeyAsync(publicKey);
        if (existingKey != null && existingKey.ApiKey == publicKey)
        {
            existingKey.CheckLocked();
            return appId;
        }

        _logger.LogWarning("Apikey was not valid. {AppId} {ApiKey}", appId, publicKey);
        throw new ApiException("Apikey was not valid", 401);
    }

    private string GetAppId(string apiKey)
    {
        try
        {
            return ApiKeyUtils.GetAppId(apiKey);
        }
        catch (Exception)
        {
            _logger.LogError("Could not parse accountname={apikey}", apiKey);
            throw new ApiException("Please supply the apikey or apisecret header with correct value.", 401);
        }
    }

    public async Task FreezeAccount(string accountName)
    {
        var storage = tenantFactory.Create(accountName);
        await storage.LockAllApiKeys(true);
    }

    public async Task UnFreezeAccount(string accountName)
    {
        var storage = tenantFactory.Create(accountName);
        await storage.LockAllApiKeys(false);
        await storage.SetAppDeletionDate(null);
    }

    public async Task<AppDeletionResult> DeleteApplicationAsync(string appId)
    {
        var storage = tenantFactory.Create(appId);
        var accountInformation = await storage.GetAccountInformation();

        if (accountInformation == null)
        {
            throw new ApiException("app_not_found", "App was not found.", 400);
        }

        if (!accountInformation.DeletedAt.HasValue || accountInformation.DeletedAt > _systemClock.UtcNow)
        {
            throw new ApiException("app_not_pending_deletion", "App was not scheduled for deletion.", 400);
        }

        await storage.DeleteAccount();

        return new AppDeletionResult(
            $"The app '{accountInformation.AccountName}' was deleted.",
            true,
            _systemClock.UtcNow.UtcDateTime,
            accountInformation.AdminEmails);
    }

    public async Task<AppDeletionResult> MarkDeleteApplicationAsync(string appId, string deletedBy, string baseUrl)
    {
        var storage = tenantFactory.Create(appId);
        var accountInformation = await storage.GetAccountInformation();
        if (accountInformation == null)
        {
            throw new ApiException("app_not_found", "App was not found.", 400);
        }
        if (accountInformation.DeletedAt.HasValue)
        {
            throw new ApiException("app_pending_deletion", "App is already pending to be deleted.", 400);
        }
        bool canDeleteImmediately = accountInformation.CreatedAt > _systemClock.UtcNow.AddDays(-3);

        if (!canDeleteImmediately)
        {
            canDeleteImmediately = !(await storage.HasUsersAsync());
        }

        if (canDeleteImmediately)
        {
            await storage.DeleteAccount();
            return new AppDeletionResult(
                $"The app '{accountInformation.AccountName}' was deleted.",
                true,
                _systemClock.UtcNow.UtcDateTime,
                accountInformation.AdminEmails);
        }

        // Lock/Freeze all API keys that have been issued.
        await storage.LockAllApiKeys(true);

        var deleteAt = _systemClock.UtcNow.AddMonths(1).UtcDateTime;
        await storage.SetAppDeletionDate(deleteAt);

        return new AppDeletionResult(
            $"The app '{accountInformation.AccountName}' will be deleted at '{deleteAt}'.",
            false,
            deleteAt,
            accountInformation.AdminEmails);
    }

    public async Task<IEnumerable<string>> GetApplicationsPendingDeletionAsync()
    {
        var storage = _globalStorageFactory.Create();
        var tenants = await storage.GetApplicationsPendingDeletionAsync();
        return tenants;
    }

    public async Task SetFeaturesAsync(string appId, ManageFeaturesDto payload)
    {
        if (payload == null)
        {
            throw new ApiException("No 'body' or 'parameters' were passed.", 400);
        }

        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ApiException($"'{nameof(appId)}' is required.", 400);
        }
        var storage = tenantFactory.Create(appId);
        await storage.SetFeaturesAsync(payload);
    }

    public async Task<AppFeatureDto> GetFeaturesAsync(string appId)
    {
        var storage = tenantFactory.Create(appId);
        var entity = await storage.GetAppFeaturesAsync();
        var dto = AppFeatureDto.FromEntity(entity);
        return dto;
    }

    private static async Task<(string original, string hashed)> SetupApiSecret(string accountName,
        ITenantStorage storage)
    {
        var secretKey = ApiKeyUtils.GeneratePrivateApiKey(accountName, "secret");
        // last 4 chars
        var pk2 = secretKey.originalApiKey.Substring(secretKey.originalApiKey.Length - 4);
        await storage.StoreApiKey(pk2, secretKey.hashedApiKey, new string[] { "token_register", "token_verify" });
        return secretKey;
    }

    private static async Task<string> SetupApiKey(string accountName, ITenantStorage storage)
    {
        // create tenant and store apikey
        var publicKey = ApiKeyUtils.GeneratePublicApiKey(accountName, "public");
        // last 4 chars
        var pk = publicKey.Substring(publicKey.Length - 4);
        await storage.StoreApiKey(pk, publicKey, new string[] { "register", "login" });
        return publicKey;
    }
}