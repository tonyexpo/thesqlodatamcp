namespace ODataRuntimeEdmSpike;

// This is a transport DTO for the spike only. It is not an EF entity and no EF
// model is used; the public EDM is assembled independently in RuntimeEdmModel.
public sealed record SalesOrder(int Id, string CustomerName, decimal NetAmount);
