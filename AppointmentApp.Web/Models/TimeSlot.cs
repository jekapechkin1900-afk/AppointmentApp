namespace AppointmentApp.Web.Models;

public class TimeSlot
{
	public string Time { get; set; }
	public string Status { get; set; } 
	public string? BookedByClientId { get; set; }
}