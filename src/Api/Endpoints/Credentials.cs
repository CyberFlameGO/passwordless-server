using System.Security.Claims;
using ApiHelpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Azure.Documents;
using Passwordless.Api.Authorization;
using Passwordless.Api.Models;
using Passwordless.Common.Models;
using Passwordless.Service;
using Passwordless.Service.AuditLog.Loggers;
using Passwordless.Service.AuditLog.Models;
using Passwordless.Service.Helpers;
using Passwordless.Service.Storage.Ef;
using static Passwordless.Service.AuditLog.AuditEventFunctions;

namespace Passwordless.Server.Endpoints;

public static class CredentialsEndpoints
{
    public static void MapCredentialsEndpoints(this WebApplication app)
    {
        app.MapPost("/credentials/delete", async (CredentialsDeleteDTO payload,
                UserCredentialsService userCredentialsService,
                IAuditLogger auditLogger,
                IAuditLogContext auditLogContext) =>
        {
            await userCredentialsService.DeleteCredential(payload.CredentialId);

            auditLogger.LogEvent(DeleteCredentialEvent("System", auditLogContext));

            return Results.NoContent();
        })
            .RequireSecretKey()
            .RequireCors("default");


        app.MapMethods("/credentials/list", new[] { "post", "get" }, async (HttpContext ctx, HttpRequest req, ITenantStorage storage, UserCredentialsService userCredentialService) =>
        {
            string userId = "";
            if (req.Method == "POST")
            {
                var payload = await req.ReadFromJsonAsync<CredentialsListDTO>();

                // if payload is empty, throw exception
                if (payload == null)
                {
                    throw new ApiException("Payload is empty", 400);
                }

                userId = payload.UserId;
            }
            else
            {
                userId = req.Query["userId"].SingleOrDefault() ?? throw new Exception("userId should have been supplied in a query string value");
            }

            // if payload is empty, throw exception
            if (userId == null)
            {
                throw new ApiException("Please supply UserId", 400);
            }

            var result = await userCredentialService.GetAllCredentials(userId);

            var res = ListResponse.Create(result);

            return Results.Ok(res);
        })
            .RequireSecretKey()
            .RequireCors("default");
    }
}