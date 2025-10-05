using System.Net.WebSockets;
using AppointmentApp.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<AppointmentService>();

var app = builder.Build();


app.UseStaticFiles();
app.UseWebSockets();

app.Map("/ws", async (HttpContext context, AppointmentService appointmentService) =>
{
	if (context.WebSockets.IsWebSocketRequest)
	{
		using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
		var clientId = Guid.NewGuid().ToString(); 

		await appointmentService.OnConnected(clientId, webSocket);

		var buffer = new byte[1024 * 4];
		WebSocketReceiveResult receiveResult;

		try
		{
			do
			{
				receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
				if (!receiveResult.CloseStatus.HasValue)
				{
					await appointmentService.ReceiveMessageAsync(clientId, receiveResult, buffer);
				}
			} while (!receiveResult.CloseStatus.HasValue);
		}
		catch (WebSocketException)
		{
			
		}
		finally
		{
			await appointmentService.OnDisconnected(clientId);
			if (webSocket.State != WebSocketState.Closed)
			{
				await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
			}
		}
	}
	else
	{
		context.Response.StatusCode = StatusCodes.Status400BadRequest;
	}
});

app.Run();
