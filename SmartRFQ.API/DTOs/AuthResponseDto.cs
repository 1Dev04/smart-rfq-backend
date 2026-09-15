namespace SmartRFQ.API.DTOs;

public record AuthResponseDto(string FullName, string Email, string Role, string? Token = null);