using API.DBContext;
using API.Interface;
using API.Model;
using API.Model.TradeNet;
using API.Service.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace API.Service
{
    public class JWTManagerService : IJWTManagerService
    {

        private readonly IConfiguration iconfiguration;
        private readonly ICommonService<TokenModel> _tokenService;
        private readonly TradeNetDbContext _tradeNetDb;
        public JWTManagerService(IConfiguration iconfiguration,
        ICommonService<TokenModel> tokenService,
        TradeNetDbContext tradeNetDb
            )
        {
            this.iconfiguration = iconfiguration;
            _tokenService = tokenService;
            _tradeNetDb = tradeNetDb;
        }
        public async Task<TokenModel?> Authenticate(API.Model.User users)
        {
            var encryptedPassword = TradenetPasswordCipher.Encrypt(users.Password);

            var tempUser = await _tradeNetDb.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.UserName == users.Name &&
                    x.Password == encryptedPassword &&
                    x.IsActive &&
                    !x.IsDeleted);

            if (tempUser == null)
            {
                return null;
            }

            var userRights = await _tradeNetDb.UserDetails
                .AsNoTracking()
                .Where(x => x.UserId == tempUser.Id)
                .ToListAsync();

            var permission = GetPermission(tempUser, userRights);

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenKey = Encoding.UTF8.GetBytes(iconfiguration["JWT:Key"] ?? "");
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, tempUser.Id.ToString()),
                new Claim(ClaimTypes.Role, permission)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddDays(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(tokenKey), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var refreshToken = Guid.NewGuid().ToString();
            var encryptedRefreshToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(refreshToken));
            var userId = tempUser.Id.ToString();
            var tokenObj = new TokenModel
            {
                Token = tokenHandler.WriteToken(token),
                RefreshToken = encryptedRefreshToken,
                UserId = userId,
                Permission = permission,
            };
            await _tokenService.Create(tokenObj);
            return tokenObj;

        }
        public TokenModel AuthenticateTradenet2(String user)
        {

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenKey = Encoding.UTF8.GetBytes(iconfiguration["JWT:Key"] ?? "");
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                new Claim(ClaimTypes.Name, user),
                new Claim(ClaimTypes.Role, "User")
                }),
                Expires = DateTime.Now.AddDays(31),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(tokenKey), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var refreshToken = Guid.NewGuid().ToString();
            var encryptedRefreshToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(refreshToken));
            var userId = user;
            var tokenObj = new TokenModel
            {
                Token = tokenHandler.WriteToken(token),
                RefreshToken = encryptedRefreshToken,
                UserId = userId,
                Permission = "User",
            };
            _tokenService.Create(tokenObj);
            return tokenObj;

        }



        public TokenModel? RefreshToken(string RefreshToken)
        {
            IQueryable<TokenModel> TokenRecord = _tokenService.Retrieve.Where(x => x.RefreshToken == RefreshToken);
            if (!TokenRecord.Any())
            {
                return null;
            }

            // Else we generate JSON Web Token
            var tempToken = TokenRecord.FirstOrDefault();
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenKey = Encoding.UTF8.GetBytes(iconfiguration["JWT:Key"] ?? "");
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                 new Claim(ClaimTypes.Name, tempToken?.UserId??"")
                }),
                Expires = DateTime.Now.AddDays(31),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(tokenKey), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var refreshToken = Guid.NewGuid().ToString();
            var encryptedRefreshToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(refreshToken));
            var userId = tempToken?.UserId ?? "";
            var tokenObj = new TokenModel { Token = tokenHandler.WriteToken(token), RefreshToken = encryptedRefreshToken, UserId = userId };
            _tokenService.Create(tokenObj);
            _tokenService.Delete(tempToken?.Token ?? "");
            return tokenObj;

        }



        private static string GetPermission(API.Model.TradeNet.User user, IReadOnlyCollection<UserDetail> userRights)
        {
            if (string.Equals(user.UserType, "Admin", StringComparison.OrdinalIgnoreCase) ||
                userRights.Any(x =>
                    string.Equals(x.Type, "Admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.SubType, "Admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.Section, "Admin", StringComparison.OrdinalIgnoreCase)))
            {
                return "Admin";
            }

            return "User";
        }

    }
}
