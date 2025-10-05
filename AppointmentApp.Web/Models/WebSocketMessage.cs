namespace AppointmentApp.Web.Models;

public class WebSocketMessage
{
	public string? Type { get; set; }
	public object? Payload { get; set; }
}
