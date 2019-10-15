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
using System.Security.Claims;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace NetCoreSendMail.Services
{
    public interface ITokenCacheFactory
    {
        TokenCache CreateForUser(ClaimsPrincipal user);
    }
}
