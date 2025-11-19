namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IStorageQueue
{

    Task ProcessQueue(string queue);
}