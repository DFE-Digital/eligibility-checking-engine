using CheckYourEligibility.Core.Boundary.Requests;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface IUsers
{
    Task<string> Create(UserData data);
}