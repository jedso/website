﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Momentum.Auth.Core.Services;
using Momentum.Users.Application.DTOs;
using Momentum.Users.Application.Requests;

namespace Momentum.Framework.Application.Services
{
    /// <summary>
    ///     Scoped service for the current user
    /// </summary>
    public class CurrentUserService : ICurrentUserService
    {
        private readonly HttpContext _httpContext;
        private readonly IMediator _mediator;
        private readonly IJwtService _jwtService;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor, IMediator mediator, IJwtService jwtService)
        {
            _httpContext = httpContextAccessor.HttpContext;
            _mediator = mediator;
            _jwtService = jwtService;
        }

        public Guid GetUserId()
        {
            var jti = GetClaims()
                .FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti);

            if (jti == null)
            {
                return Guid.Empty;
            }

            var userId = Guid.Parse(jti.Value);
            return userId;
        }

        public async Task<UserDto> GetUser()
        {
            var userId = GetUserId();

            if (userId == Guid.Empty)
            {
                throw new Exception("No current user");
            }

            // Skips MediatR as Core cannot reference Application (circular dependency)
            return await _mediator.Send(new GetUserByIdQuery
            {
                Id = userId
            });
        }

        public List<Claim> GetClaims()
        {
            var accessToken = GetBearerToken();

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new Exception("No access token");
            }

            var claims = _jwtService.ExtractClaims(accessToken);

            if (!claims.Any())
            {
                throw new Exception("User has no claims");
            }

            return claims;
        }

        public string GetBearerToken()
        {
            var authorizationHeader = _httpContext.Request.Headers[HeaderNames.Authorization]
                .ToString();
            var bearerToken = authorizationHeader.Replace("Bearer ", "");
            return bearerToken;
        }

        public RolesDto GetRolesFromToken()
        {
            var claims = GetClaims();

            var rolesClaim = claims.FirstOrDefault(x => x.Type == "roles");

            if (rolesClaim == null)
            {
                throw new ArgumentNullException(nameof(rolesClaim), "Token has no roles claim");
            }

            if (Enum.TryParse<RolesDto>(rolesClaim.Value, out var roles))
            {
                return roles;
            }

            throw new Exception("Error parsing roles from token");
        }

        public bool HasRole(RolesDto role)
        {
            var userRoles = GetRolesFromToken();

            return (userRoles & role) == role;
        }
    }
}