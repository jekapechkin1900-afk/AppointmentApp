using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AppointmentApp.Web.Models;

namespace AppointmentApp.Web.Services;

public class AppointmentService
{
	private readonly ConcurrentDictionary<string, WebSocket> _clients = [];
	private readonly ConcurrentDictionary<string, List<TimeSlot>> _schedule;

	private readonly object _lock = new();

	public AppointmentService()
	{
		_schedule = [];
		InitializeSchedule();
	}

	public void ResetSchedule()
	{
		lock (_lock)
		{
			InitializeSchedule();
			var resetMessage = new WebSocketMessage { Type = "initialState", Payload = _schedule };
			BroadcastMessageAsync(resetMessage);
		}
	}

	private void InitializeSchedule()
	{
		_schedule.Clear();
		_schedule.TryAdd("Терапевт",
		[
			new() { Time = "09:00", Status = "Free" },
			new() { Time = "09:30", Status = "Free" },
			new() { Time = "10:00", Status = "Booked" }, 
            new() { Time = "10:30", Status = "Free" },
		]);
		_schedule.TryAdd("Стоматолог",
		[
			new() { Time = "09:00", Status = "Free" },
			new() { Time = "09:30", Status = "Free" },
			new() { Time = "10:00", Status = "Booked" },
			new() { Time = "10:30", Status = "Free" },
		]);
		_schedule.TryAdd("Хирург",
		[
			new() { Time = "14:00", Status = "Free" },
			new() { Time = "14:30", Status = "Free" },
			new() { Time = "15:00", Status = "Free" },
		]);
	}

	public async Task OnConnected(string clientId, WebSocket socket)
	{
		_clients.TryAdd(clientId, socket);
		var initialStateMessage = new WebSocketMessage { Type = "initialState", Payload = _schedule };
		var jsonMessage = JsonSerializer.Serialize(initialStateMessage);
		var bytes = Encoding.UTF8.GetBytes(jsonMessage);
		await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
	}

	public async Task OnDisconnected(string clientId)
	{
		_clients.TryRemove(clientId, out _);
	}

	public async Task ReceiveMessageAsync(string clientId, WebSocketReceiveResult result, byte[] buffer)
	{
		var messageString = Encoding.UTF8.GetString(buffer, 0, result.Count);
		var message = JsonSerializer.Deserialize<JsonDocument>(messageString);

		if (message.RootElement.TryGetProperty("type", out var type))
		{
			var messageType = type.GetString();

			if (messageType == "book")
			{
				var payload = message.RootElement.GetProperty("payload");
				var doctor = payload.GetProperty("doctor").GetString();
				var time = payload.GetProperty("time").GetString();
				BookSlot(doctor, time, clientId);
			}
			else if (messageType == "reset")
			{
				ResetSchedule();
			}
		}
	}

	private void BookSlot(string doctor, string time, string clientId)
	{
		lock (_lock)
		{
			if (_schedule.TryGetValue(doctor, out var slots))
			{
				var slot = slots.FirstOrDefault(s => s.Time == time);
				if (slot != null && slot.Status == "Free")
				{
					slot.Status = "Booked";
					slot.BookedByClientId = clientId;

					var updatePayload = new { doctor, time, status = "Booked", bookedByClientId = clientId };
					var updateMessage = new WebSocketMessage { Type = "update", Payload = updatePayload };
					BroadcastMessageAsync(updateMessage);
				}
			}
		}
	}

	private async void BroadcastMessageAsync(WebSocketMessage message)
	{
		var messageString = JsonSerializer.Serialize(message);
		var bytes = Encoding.UTF8.GetBytes(messageString);
		var segment = new ArraySegment<byte>(bytes);

		var tasks = _clients.Values.Select(socket =>
			socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None));

		await Task.WhenAll(tasks);
	}
}