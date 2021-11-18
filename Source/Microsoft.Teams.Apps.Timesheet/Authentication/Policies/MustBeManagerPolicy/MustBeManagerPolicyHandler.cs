﻿// <copyright file="MustBeManagerPolicyHandler.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.Teams.Apps.Timesheet.Authentication
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Options;
    using Microsoft.Teams.Apps.Timesheet.Models;
    using Microsoft.Teams.Apps.Timesheet.Services.MicrosoftGraph;
    using Task = System.Threading.Tasks.Task;

    /// <summary>
    /// This authorization handler is created to handle manager access policy.
    /// The class implements AuthorizationHandler for handling MustBeManagerPolicyRequirement authorization.
    /// </summary>
    public class MustBeManagerPolicyHandler : IAuthorizationHandler
    {
        /// <summary>
        /// A set of key/value application configuration properties for caching settings.
        /// </summary>
        private readonly IOptions<BotSettings> botOptions;

        /// <summary>
        /// Cache for storing authorization result.
        /// </summary>
        private readonly IMemoryCache memoryCache;

        /// <summary>
        /// Instance of user Graph service.
        /// </summary>
        private readonly IUsersService usersService;

        /// <summary>
        /// Initializes a new instance of the <see cref="MustBeManagerPolicyHandler"/> class.
        /// </summary>
        /// <param name="memoryCache">Memory cache instance for caching authorization result.</param>
        /// <param name="usersService">Instance of user Graph service.</param>
        /// <param name="botOptions">A set of key/value application configuration properties for caching settings.</param>
        public MustBeManagerPolicyHandler(
           IMemoryCache memoryCache,
           IUsersService usersService,
           IOptions<BotSettings> botOptions)
        {
            this.memoryCache = memoryCache;
            this.usersService = usersService;
            this.botOptions = botOptions;
        }

        /// <summary>
        /// This method handles the authorization requirement.
        /// </summary>
        /// <param name="context">AuthorizationHandlerContext instance.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task HandleAsync(AuthorizationHandlerContext context)
        {
            context = context ?? throw new ArgumentNullException(nameof(context));

            foreach (var c in context.User.Claims)
            {
                Console.WriteLine($"claim type: {c.Type}");
                Console.WriteLine($"claim value: {c.Value}");
            }

            var oidClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";

            var oidClaim = context.User.Claims.FirstOrDefault(p => oidClaimType == p.Type);

            foreach (var requirement in context.Requirements)
            {
                if (requirement is MustBeManagerPolicyRequirement)
                {
                    // Check if manager has reportees.
                    if (await this.ValidateManagerReporteesAsync(oidClaim.Value))
                    {
                        context.Succeed(requirement);
                    }
                }
            }
        }

        /// <summary>
        /// Check if a manager have reportee or not.
        /// </summary>
        /// <param name="userAadObjectId">The user's Azure Active Directory object id.</param>
        /// <returns>The flag indicates that manger have reportee or not.</returns>
        private async Task<bool> ValidateManagerReporteesAsync(string userAadObjectId)
        {
            // The key is generated by user object Id.
            bool isEntryAvailableInCache = this.memoryCache.TryGetValue(this.GetCacheKey(userAadObjectId), out bool isValidManager);

            if (!isEntryAvailableInCache)
            {
                var reportees = await this.usersService.GetMyReporteesAsync(search: string.Empty);
                isValidManager = reportees.Any();
                this.memoryCache.Set(this.GetCacheKey(userAadObjectId), isValidManager, TimeSpan.FromHours(this.botOptions.Value.ManagerReporteesCacheDurationInHours));
            }

            return isValidManager;
        }

        /// <summary>
        /// Generate key by user object Id.
        /// </summary>
        /// <param name="userAadObjectId">The user's Azure Active Directory object Id.</param>
        /// <returns>Generated key.</returns>
        private string GetCacheKey(string userAadObjectId)
        {
            return $"manager_{userAadObjectId}";
        }
    }
}
