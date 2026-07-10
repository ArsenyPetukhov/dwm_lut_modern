using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DwmLutModern.Services
{
	public sealed class CommandResponse
	{
		public bool Success { get; init; }
		public int ExitCode { get; init; }
		public string Output { get; init; } = string.Empty;
		public string Error { get; init; } = string.Empty;

		public static CommandResponse Ok(string output = "") =>
			new CommandResponse { Success = true, Output = output };

		public static CommandResponse Fail(string error, int exitCode = 1) =>
			new CommandResponse { Success = false, ExitCode = exitCode, Error = error };
	}

	internal sealed class CommandRequest
	{
		public string[] Arguments { get; init; } = Array.Empty<string>();
	}

	/// <summary>
	/// Duplex, current-user-only command channel. The process that owns the singleton mutex hosts
	/// the server; every secondary launch sends its original arguments and waits for the result.
	/// </summary>
	public sealed class SingleInstanceCommandChannel : IAsyncDisposable
	{
		private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		};

		private readonly string _pipeName;
		private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
		private Task? _serverTask;

		public SingleInstanceCommandChannel(string pipeName)
		{
			_pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
		}

		public void Start(Func<string[], Task<CommandResponse>> handler)
		{
			if (handler == null) throw new ArgumentNullException(nameof(handler));
			if (_serverTask != null) throw new InvalidOperationException("Command channel is already running.");
			_serverTask = Task.Run(() => ServeAsync(handler, _cancellation.Token));
		}

		public async Task<CommandResponse> SendAsync(
			string[] arguments,
			TimeSpan timeout,
			CancellationToken cancellationToken = default)
		{
			using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			linked.CancelAfter(timeout);
			try
			{
				using var client = new NamedPipeClientStream(
					".", _pipeName, PipeDirection.InOut,
					PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
				await client.ConnectAsync(linked.Token).ConfigureAwait(false);

				using var reader = new StreamReader(client, leaveOpen: true);
				using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
				await writer.WriteLineAsync(JsonSerializer.Serialize(
					new CommandRequest { Arguments = arguments }, JsonOptions)).ConfigureAwait(false);

				var line = await reader.ReadLineAsync(linked.Token).ConfigureAwait(false);
				if (string.IsNullOrWhiteSpace(line)) return CommandResponse.Fail("The running app returned no response.");
				return JsonSerializer.Deserialize<CommandResponse>(line, JsonOptions)
					?? CommandResponse.Fail("The running app returned an invalid response.");
			}
			catch (OperationCanceledException)
			{
				return CommandResponse.Fail("Timed out contacting the running DWM LUT Modern instance.", 2);
			}
			catch (Exception ex)
			{
				return CommandResponse.Fail("Couldn't contact the running DWM LUT Modern instance: " + ex.Message, 2);
			}
		}

		private async Task ServeAsync(Func<string[], Task<CommandResponse>> handler, CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					using var server = new NamedPipeServerStream(
						_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
						PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
					await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

					using var reader = new StreamReader(server, leaveOpen: true);
					using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };
					var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
					var request = string.IsNullOrWhiteSpace(line)
						? null
						: JsonSerializer.Deserialize<CommandRequest>(line, JsonOptions);

					CommandResponse response;
					try
					{
						response = request == null
							? CommandResponse.Fail("Invalid command request.")
							: await handler(request.Arguments).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						response = CommandResponse.Fail("Command failed: " + ex.Message);
					}

					await writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions)).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					break;
				}
				catch
				{
					// Keep the owner alive. A malformed/broken client must not tear down command control.
				}
			}
		}

		public async ValueTask DisposeAsync()
		{
			_cancellation.Cancel();
			if (_serverTask != null)
			{
				try { await _serverTask.ConfigureAwait(false); }
				catch (OperationCanceledException) { }
			}
			_cancellation.Dispose();
		}
	}
}
