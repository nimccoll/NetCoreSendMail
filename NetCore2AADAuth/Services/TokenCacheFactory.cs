//===============================================================================
// Microsoft Premier Support for Developers
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
using NetCore2AADAuth.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Clients.ActiveDirectory;


namespace NetCore2AADAuth.Services
{
    public class TokenCacheFactory : ITokenCacheFactory

    {
        private readonly IDistributedCache _distributedCache;
        private readonly IDataProtectionProvider _dataProtectionProvider;
        //Token cache is cached in-memory in this instance to avoid loading data multiple times during the request
        //For this reason this factory should always be registered as Scoped
        private TokenCache _cachedTokenCache;
        private string _cachedTokenCacheUserId;

        public TokenCacheFactory(IDistributedCache distributedCache, IDataProtectionProvider dataProtectionProvider)
        {
            _distributedCache = distributedCache;
            _dataProtectionProvider = dataProtectionProvider;
        }

        public TokenCache CreateForUser(ClaimsPrincipal user)
        {
            string userId = user.GetObjectId();

            if (_cachedTokenCache != null)
            {
                // Guard for accidental re-use across requests
                if (userId != _cachedTokenCacheUserId)
                {
                    throw new Exception("The cached token cache is for a different user! Make sure the token cache factory is registered as Scoped!");
                }

                return _cachedTokenCache;
            }

            _cachedTokenCache = new AdalDistributedTokenCache(
                _distributedCache, _dataProtectionProvider, userId);
            _cachedTokenCacheUserId = userId;
            return _cachedTokenCache;
        }
    }
}
