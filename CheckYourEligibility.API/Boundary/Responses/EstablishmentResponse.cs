public class EstablishmentResponse
{
    public List<EstablishmentResponseItem> Data { get; set; }
}

public class EstablishmentResponseItem
{
    public int URN { get; set; }    
    public string Name { get; set; }
}