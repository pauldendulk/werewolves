namespace WerewolvesAPI.DTOs;

public class CreateCheckoutSessionRequest
{
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
}
