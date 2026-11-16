using GLMS.Contracts;
using GLMS.Web.Models;

namespace GLMS.Api.Services;

public interface ITokenService
{
    AccountTokenResponse Create(ApplicationUser user);
}
