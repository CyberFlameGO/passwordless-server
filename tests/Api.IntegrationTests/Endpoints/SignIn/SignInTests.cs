using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using Fido2NetLib;
using Fido2NetLib.Objects;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Passwordless.Api.Endpoints;
using Passwordless.Api.IntegrationTests.Helpers;
using Passwordless.Api.IntegrationTests.Helpers.App;
using Passwordless.Api.IntegrationTests.Helpers.User;
using Passwordless.Common.Models.Apps;
using Passwordless.Service.Models;
using Passwordless.Service.Storage.Ef;
using Xunit;
using Xunit.Abstractions;

namespace Passwordless.Api.IntegrationTests.Endpoints.SignIn;

public class SignInTests(ITestOutputHelper testOutput, PasswordlessApiFixture apiFixture)
    : IClassFixture<PasswordlessApiFixture>
{
    [Fact]
    public async Task I_can_retrieve_assertion_options_to_begin_sign_in()
    {
        // Arrange
        await using var api = await apiFixture.CreateApiAsync(new PasswordlessApiOptions
        {

            TestOutput = testOutput
        });
        using var client = api.CreateClient().AddPublicKey().AddSecretKey().AddUserAgent();

        var request = new SignInBeginDTO { Origin = PasswordlessApi.OriginUrl, RPID = PasswordlessApi.RpId };

        // Act
        using var response = await client.PostAsJsonAsync("/signin/begin", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var signInResponse = await response.Content.ReadFromJsonAsync<SessionResponse<Fido2NetLib.AssertionOptions>>();

        signInResponse.Should().NotBeNull();
        signInResponse!.Session.Should().StartWith("session_");
        signInResponse.Data.RpId.Should().Be(request.RPID);
        signInResponse.Data.Status.Should().Be("ok");
    }

    [Fact]
    public async Task I_can_retrieve_my_passkey_after_registering_and_receive_a_sign_in_token()
    {
        // Arrange
        await using var api = await apiFixture.CreateApiAsync(new PasswordlessApiOptions
        {

            TestOutput = testOutput
        });
        using var httpClient = api.CreateClient().AddPublicKey().AddSecretKey().AddUserAgent();

        using var driver = WebDriverFactory.GetDriver(PasswordlessApi.OriginUrl);
        await httpClient.RegisterNewUser(driver);

        var signInBeginResponse = await httpClient.PostAsJsonAsync("/signin/begin", new SignInBeginDTO { Origin = PasswordlessApi.OriginUrl, RPID = PasswordlessApi.RpId });
        var signInBegin = await signInBeginResponse.Content.ReadFromJsonAsync<SessionResponse<Fido2NetLib.AssertionOptions>>();
        var authenticatorAssertionRawResponse = await driver.GetCredentialsAsync(signInBegin!.Data);

        // Act
        using var signInCompleteResponse = await httpClient.PostAsJsonAsync("/signin/complete", new SignInCompleteDTO
        {
            Origin = PasswordlessApi.OriginUrl,
            RPID = PasswordlessApi.RpId,
            Response = authenticatorAssertionRawResponse,
            Session = signInBegin.Session
        });

        // Assert
        signInCompleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var signInTokenResponse = await signInCompleteResponse.Content.ReadFromJsonAsync<TokenResponse>();
        signInTokenResponse.Should().NotBeNull();
        signInTokenResponse!.Token.Should().StartWith("verify_");
    }

    [Fact]
    public async Task I_can_retrieve_my_passkey_after_registering_and_receive_a_valid_sign_in_token()
    {
        // Arrange
        await using var api = await apiFixture.CreateApiAsync(new PasswordlessApiOptions
        {

            TestOutput = testOutput
        });
        using var httpClient = api.CreateClient().AddPublicKey().AddSecretKey().AddUserAgent();

        using var driver = WebDriverFactory.GetDriver(PasswordlessApi.OriginUrl);
        await httpClient.RegisterNewUser(driver);

        var signInBeginResponse = await httpClient.PostAsJsonAsync("/signin/begin", new SignInBeginDTO { Origin = PasswordlessApi.OriginUrl, RPID = PasswordlessApi.RpId });
        var signInBegin = await signInBeginResponse.Content.ReadFromJsonAsync<SessionResponse<Fido2NetLib.AssertionOptions>>();

        var authenticatorAssertionRawResponse = await driver.GetCredentialsAsync(signInBegin!.Data);

        // Act
        using var signInCompleteResponse = await httpClient.PostAsJsonAsync("/signin/complete", new SignInCompleteDTO
        {
            Origin = PasswordlessApi.OriginUrl,
            RPID = PasswordlessApi.RpId,
            Response = authenticatorAssertionRawResponse,
            Session = signInBegin.Session
        });

        // Assert
        signInCompleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var signInTokenResponse = await signInCompleteResponse.Content.ReadFromJsonAsync<TokenResponse>();
        signInTokenResponse.Should().NotBeNull();
        signInTokenResponse!.Token.Should().StartWith("verify_");

        var verifySignInResponse = await httpClient.PostAsJsonAsync("/signin/verify", new SignInVerifyDTO { Token = signInTokenResponse.Token });
        verifySignInResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task I_receive_an_error_message_when_sending_an_unrecognized_passkey()
    {
        // Arrange
        await using var api = await apiFixture.CreateApiAsync(new PasswordlessApiOptions
        {

            TestOutput = testOutput
        });
        using var _httpClient = api.CreateClient().AddPublicKey().AddSecretKey().AddUserAgent();

        using var options = await _httpClient.PostAsJsonAsync("/signin/begin", new { Origin = PasswordlessApi.OriginUrl, RPID = PasswordlessApi.RpId });
        var response = await options.Content.ReadFromJsonAsync<SessionResponse<Fido2NetLib.AssertionOptions>>();
        var payloadWithUnrecognizedPasskey = new
        {
            Origin = PasswordlessApi.OriginUrl,
            RPID = PasswordlessApi.RpId,
            Session = response!.Session,
            Response = new
            {
                Id = "LcVLKA2QkfwzvuSTxIIyFVTJ9IopE57xTYvJ_0Nx9nk",
                RawId = "LcVLKA2QkfwzvuSTxIIyFVTJ9IopE57xTYvJ_0Nx9nk",
                Response = new
                {
                    AuthenticatorData = "8egiesVpgMwlnLcY0N3ldtvrzKGUPb763GkSYC4CzTkFAAAAAA",
                    ClientDataJson =
                        "eyJ0eXBlIjoid2ViYXV0aG4uZ2V0IiwiY2hhbGxlbmdlIjoiYmhPUllmRlR5S1hfdEtDQkFQbVVKdyIsIm9yaWdpbiI6Imh0dHBzOi8vYWRtaW4ucGFzc3dvcmRsZXNzLmRldiIsImNyb3NzT3JpZ2luIjpmYWxzZX0",
                    Signature =
                        "MEUCIQDiTSGMfKb_qZQDB0J8KFFZeAOrvCZEF2yi6MgoUNwkTgIgHYfe8LKPg-INMK9NxJfPaCdNsRUtP2DMhDKraJAOvzk"
                },
                type = "public-key"
            }
        };

        // Act
        using var completeResponse = await _httpClient.PostAsJsonAsync("/signin/complete", payloadWithUnrecognizedPasskey);

        // Assert
        completeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await completeResponse.Content.ReadAsStringAsync();
        AssertHelper.AssertEqualJson(
            // lang=json
            """
             {
               "type": "https://docs.passwordless.dev/guide/errors.html#unknown_credential",
               "title": "We don't recognize the passkey you sent us.",
               "status": 400,
               "credentialId": "LcVLKA2QkfwzvuSTxIIyFVTJ9IopE57xTYvJ_0Nx9nk",
               "errorCode": "unknown_credential"
             }
             """, body);
    }

    [Fact]
    public async Task An_expired_apps_token_keys_should_be_removed_when_a_request_is_made()
    {
        // Arrange
        await using var api = await apiFixture.CreateApiAsync(new PasswordlessApiOptions
        {

            TestOutput = testOutput
        });
        using var httpClient = api.CreateClient().AddPublicKey().AddSecretKey().AddUserAgent();

        var applicationName = $"test{Guid.NewGuid():N}";
        using var client = api.CreateClient().AddManagementKey();
        using var createApplicationMessage = await client.CreateApplicationAsync(applicationName);
        var accountKeysCreation = await createApplicationMessage.Content.ReadFromJsonAsync<CreateAppResultDto>();
        client.AddPublicKey(accountKeysCreation!.ApiKey1);
        client.AddSecretKey(accountKeysCreation.ApiSecret1);
        using var driver = WebDriverFactory.GetDriver(PasswordlessApi.OriginUrl);
        await client.RegisterNewUser(driver);
        api.Time.Advance(TimeSpan.FromDays(31));

        // Act
        using var response = await client.SignInUser(driver);

        // Assert
        using var scope = api.Services.CreateScope();
        var tokenKeys = await scope.ServiceProvider.GetRequiredService<ITenantStorageFactory>().Create(applicationName).GetTokenKeys();
        tokenKeys.Should().NotBeNull();
        tokenKeys.Any(x => x.CreatedAt < (DateTime.UtcNow.AddDays(-30))).Should().BeFalse();
        tokenKeys.Any(x => x.CreatedAt >= (DateTime.UtcNow.AddDays(-30))).Should().BeTrue();
    }

    [Fact]
    public async Task I_receive_a_sign_in_token_for_a_valid_user_id()
    {
        // Arrange
        await using var api = await apiFixture.CreateApiAsync(new PasswordlessApiOptions
        {

            TestOutput = testOutput
        });
        using var httpClient = api.CreateClient().AddPublicKey().AddSecretKey().AddUserAgent();

        using var client = api.CreateClient().AddManagementKey();
        using var createApplicationMessage = await client.CreateApplicationAsync();
        var userId = $"user{Guid.NewGuid():N}";
        var accountKeysCreation = await createApplicationMessage.Content.ReadFromJsonAsync<CreateAppResultDto>();
        client.AddPublicKey(accountKeysCreation!.ApiKey1)
            .AddSecretKey(accountKeysCreation.ApiSecret1)
            .AddUserAgent();

        // Act
        using var signInGenerateTokenResponse = await client.PostAsJsonAsync("signin/generate-token", new SigninTokenRequest
        {
            UserId = userId,
            Origin = PasswordlessApi.OriginUrl,
            RPID = PasswordlessApi.RpId
        });

        // Assert
        signInGenerateTokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var generateToken = await signInGenerateTokenResponse.Content.ReadFromJsonAsync<SigninEndpoints.SigninTokenResponse>();
        generateToken.Should().NotBeNull();
        generateToken!.Token.Should().StartWith("verify_");

        var verifySignInResponse = await client.PostAsJsonAsync("/signin/verify", new SignInVerifyDTO { Token = generateToken.Token, Origin = PasswordlessApi.OriginUrl, RPID = PasswordlessApi.RpId });
        verifySignInResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task I_receive_an_api_exception_when_using_an_expired_token()
    {
        // Arrange
        const int timeToLive = 120;
        await using var api = await apiFixture.CreateApiAsync(new PasswordlessApiOptions
        {

            TestOutput = testOutput
        });
        using var httpClient = api.CreateClient().AddPublicKey().AddSecretKey().AddUserAgent();

        using var client = api.CreateClient().AddManagementKey();
        using var createApplicationMessage = await client.CreateApplicationAsync();
        var userId = $"user{Guid.NewGuid():N}";
        var accountKeysCreation = await createApplicationMessage.Content.ReadFromJsonAsync<CreateAppResultDto>();
        client.AddPublicKey(accountKeysCreation!.ApiKey1)
            .AddSecretKey(accountKeysCreation.ApiSecret1)
            .AddUserAgent();
        using var signInGenerateTokenResponse = await client.PostAsJsonAsync("signin/generate-token", new SigninTokenRequest
        {
            UserId = userId,
            TimeToLiveSeconds = timeToLive,
            Origin = PasswordlessApi.OriginUrl,
            RPID = PasswordlessApi.RpId
        });
        var generateToken = await signInGenerateTokenResponse.Content.ReadFromJsonAsync<SigninEndpoints.SigninTokenResponse>();
        generateToken.Should().NotBeNull();
        generateToken!.Token.Should().StartWith("verify_");

        api.Time.Advance(TimeSpan.FromSeconds(timeToLive + 10));

        // Act
        var verifySignInResponse = await client.PostAsJsonAsync("/signin/verify", new SignInVerifyDTO { Token = generateToken.Token, Origin = PasswordlessApi.OriginUrl, RPID = PasswordlessApi.RpId });

        // Assert
        verifySignInResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problemDetails = await verifySignInResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Extensions["errorCode"]!.ToString().Should().Be("expired_token");
        problemDetails.Status.Should().Be(403);
        problemDetails.Title.Should().Be("The token expired 10 seconds ago.");
    }

    [Fact]
    public async Task I_can_create_an_authentication_configuration()
    {
        // Arrange
        await using var api = await apiFixture.CreateApiAsync(new PasswordlessApiOptions { TestOutput = testOutput });
        using var client = api.CreateClient().AddManagementKey();
        var applicationName = CreateAppHelpers.GetApplicationName();

        using var appCreationResponse = await client.CreateApplicationAsync(applicationName);
        var keysCreation = await appCreationResponse.Content.ReadFromJsonAsync<CreateAppResultDto>();
        _ = client.AddSecretKey(keysCreation!.ApiSecret1);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "/signin/authentication-configuration/new");
        request.Content = new StringContent(
            // lang=json
            """
            {
              "timeToLive": "1.01:01:01",
              "purpose": "purpose1",
              "userVerificationRequirement": "Discouraged"
            }
            """,
            Encoding.UTF8,
            MediaTypeNames.Application.Json
        );

        using var enableResponse = await client.SendAsync(request);

        // Assert
        enableResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var getConfigResponse = await client.GetFromJsonAsync<GetAuthenticationScopesResult>("signin/authentication-configurations");
        getConfigResponse.Should().NotBeNull();
        getConfigResponse!.Scopes.Should().Contain(x => x.Purpose == "purpose1");
    }

    [Fact]
    public async Task I_can_get_the_default_sign_in_authentication_configuration_without_changing_anything()
    {
        // Arrange
        await using var api = await apiFixture.CreateApiAsync(new PasswordlessApiOptions { TestOutput = testOutput });
        using var client = api.CreateClient().AddManagementKey();
        var applicationName = CreateAppHelpers.GetApplicationName();

        using var appCreationResponse = await client.CreateApplicationAsync(applicationName);
        var keysCreation = await appCreationResponse.Content.ReadFromJsonAsync<CreateAppResultDto>();
        _ = client.AddSecretKey(keysCreation!.ApiSecret1);

        // Act
        var getConfigResponse = await client.GetFromJsonAsync<AuthenticationConfigurationDto>("signin/authentication-configuration/sign-in");

        // Assert
        getConfigResponse.Should().NotBeNull();
        getConfigResponse!.Should().BeEquivalentTo(AuthenticationConfigurationDto.SignIn(applicationName));
    }

    [Fact]
    public async Task I_can_get_the_default_step_up_authentication_configuration_without_changing_anything()
    {
        // Arrange
        await using var api = await apiFixture.CreateApiAsync(new PasswordlessApiOptions { TestOutput = testOutput });
        using var client = api.CreateClient().AddManagementKey();
        var applicationName = CreateAppHelpers.GetApplicationName();

        using var appCreationResponse = await client.CreateApplicationAsync(applicationName);
        var keysCreation = await appCreationResponse.Content.ReadFromJsonAsync<CreateAppResultDto>();
        _ = client.AddSecretKey(keysCreation!.ApiSecret1);

        // Act
        var getConfigResponse = await client.GetFromJsonAsync<AuthenticationConfigurationDto>("signin/authentication-configuration/step-up");

        // Assert
        getConfigResponse.Should().NotBeNull();
        getConfigResponse!.Should().BeEquivalentTo(AuthenticationConfigurationDto.StepUp(applicationName));
    }

    [Fact]
    public async Task I_can_get_a_not_found_for_a_non_preset_and_nonexistent_configuration()
    {
        // Arrange
        await using var api = await apiFixture.CreateApiAsync(new PasswordlessApiOptions { TestOutput = testOutput });
        using var client = api.CreateClient().AddManagementKey();
        var applicationName = CreateAppHelpers.GetApplicationName();

        using var appCreationResponse = await client.CreateApplicationAsync(applicationName);
        var keysCreation = await appCreationResponse.Content.ReadFromJsonAsync<CreateAppResultDto>();
        _ = client.AddSecretKey(keysCreation!.ApiSecret1);

        // Act
        var getConfigResponse = await client.GetAsync($"signin/authentication-configuration/random");

        // Assert
        getConfigResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task I_can_delete_a_configuration_I_created()
    {
        // Arrange
        await using var api = await apiFixture.CreateApiAsync(new PasswordlessApiOptions { TestOutput = testOutput });
        using var client = api.CreateClient().AddManagementKey();
        var applicationName = CreateAppHelpers.GetApplicationName();

        using var appCreationResponse = await client.CreateApplicationAsync(applicationName);
        var keysCreation = await appCreationResponse.Content.ReadFromJsonAsync<CreateAppResultDto>();
        _ = client.AddSecretKey(keysCreation!.ApiSecret1);

        const string purpose = "purpose1";

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/signin/authentication-configuration/new");
        createRequest.Content = new StringContent(
            // lang=json
            $$"""
            {
              "timeToLive": "1.01:01:01",
              "purpose": "{{purpose}}",
              "userVerificationRequirement": "Discouraged"
            }
            """,
            Encoding.UTF8,
            MediaTypeNames.Application.Json
        );
        await client.SendAsync(createRequest);

        // Act
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "signin/authentication-configuration");
        deleteRequest.Content = new StringContent(
            // lang=json
            $$"""
              {
                "purpose": "{{purpose}}"
              }
              """,
            Encoding.UTF8,
            MediaTypeNames.Application.Json
        );
        using var deleteResponse = await client.SendAsync(deleteRequest);

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task I_can_edit_a_configuration_I_created()
    {
        // Arrange
        await using var api = await apiFixture.CreateApiAsync(new PasswordlessApiOptions { TestOutput = testOutput });
        using var client = api.CreateClient().AddManagementKey();
        var applicationName = CreateAppHelpers.GetApplicationName();

        using var appCreationResponse = await client.CreateApplicationAsync(applicationName);
        var keysCreation = await appCreationResponse.Content.ReadFromJsonAsync<CreateAppResultDto>();
        _ = client.AddSecretKey(keysCreation!.ApiSecret1);

        const string purpose = "purpose1";
        const string timeToLiveString = "1.01:01:01";
        var timeToLive = TimeSpan.Parse(timeToLiveString);
        const string uvString = "Discouraged";

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/signin/authentication-configuration/new");
        createRequest.Content = new StringContent(
            // lang=json
            $$"""
            {
              "timeToLive": "{{timeToLiveString}}",
              "purpose": "{{purpose}}",
              "userVerificationRequirement": "{{uvString}}"
            }
            """,
            Encoding.UTF8,
            MediaTypeNames.Application.Json
        );
        await client.SendAsync(createRequest);

        // Act
        using var editResponse = await client.PostAsJsonAsync("signin/authentication-configuration", new SetAuthenticationScopeRequest
        {
            Purpose = purpose,
            TimeToLive = timeToLive.Add(TimeSpan.FromDays(1)),
            UserVerificationRequirement = UserVerificationRequirement.Preferred
        });

        // Assert
        editResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getConfigResponse = await client.GetFromJsonAsync<GetAuthenticationScopesResult>("signin/authentication-configurations");
        getConfigResponse.Should().NotBeNull();
        getConfigResponse!.Scopes.Should().Contain(x => x.Purpose == purpose);
        var createdPurpose = getConfigResponse.Scopes.First(x => x.Purpose == purpose);
        createdPurpose.TimeToLive.Should().Be((int)timeToLive.Add(TimeSpan.FromDays(1)).TotalSeconds);
        createdPurpose.UserVerificationRequirement.Should().Be(UserVerificationRequirement.Preferred.ToEnumMemberValue());
    }
}