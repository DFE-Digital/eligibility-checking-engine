// Ignore Spelling: Fsm

using CheckYourEligibility.API.Boundary.Requests;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IUsers
{
    Task<string> Create(UserData data);
}