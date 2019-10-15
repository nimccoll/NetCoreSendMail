//===============================================================================
// Microsoft FastTrack for Azure
// ASP.Net Core 2 Send Mail
//===============================================================================
// Copyright © Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===============================================================================
using System;
using System.Security.Claims;

namespace NetCoreSendMail.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// Gets the user's Azure AD object id
        /// </summary>
        public static string GetObjectId(this ClaimsPrincipal principal)
        {
            if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal));
            }

            return principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");
        }
    }
}
