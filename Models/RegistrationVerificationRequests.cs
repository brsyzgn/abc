namespace ORBIT.API.Models;

public record VerifyRegistrationRequest(string Email, string Code);
public record ResendRegistrationCodeRequest(string Email);
