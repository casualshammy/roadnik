using Ax.Fw.Crypto;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Common.ReqRes.Udp;
using Roadnik.Interfaces;
using Roadnik.Server.Interfaces;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Roadnik.Server.Modules.UdpServer;

internal interface IUdpServer { }

internal class UdpServerImpl : IUdpServer, IAppModule<IUdpServer>
{
  public static IUdpServer ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      ISettingsController _settingsController,
      IReadOnlyLifetime _lifetime,
      ILog _log,
      IRoomsController _roomsController)
      => new UdpServerImpl(_settingsController, _lifetime, _log["udp-server"], _roomsController));
  }

  private UdpServerImpl(
    ISettingsController _settingsController,
    IReadOnlyLifetime _lifetime,
    ILog _log,
    IRoomsController _roomsController)
  {
    var confScheduler = new EventLoopScheduler();

    _settingsController.Settings
      .DistinctUntilChanged(_ => HashCode.Combine(_?.IpBind, _?.PortBind, _?.UdpPrivateKey, _?.UdpPublicKey))
      .HotAlive(_lifetime, confScheduler, (_conf, _life) =>
      {
        if (_conf == null)
          return;

        if (_conf.UdpPrivateKey.IsNullOrWhiteSpace())
        {
          _log.Warn($"Private key is not set - udp server is inactive");
          return;
        }
        if (_conf.UdpPublicKey.IsNullOrWhiteSpace())
        {
          _log.Warn($"Public key is not set - udp server is inactive");
          return;
        }

        var rsaAes = _life.ToDisposeOnEnded(new RsaAesGcm(null, _conf.UdpPrivateKey, null));

        _log.Info($"Starting server on {_conf.IpBind}:{_conf.PortBind}...");
        _life.DoOnEnded(() => _log.Info($"Server on {_conf.IpBind}:{_conf.PortBind} is stopped"));

        var endpoint = new IPEndPoint(IPAddress.Parse(_conf.IpBind), _conf.PortBind);
        var udpClient = _life.ToDisposeOnEnded(new UdpClient());
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.Client.Bind(endpoint);

        var thread = new Thread(async () =>
        {
          while (!_life.IsCancellationRequested)
          {
            try
            {
              var udpResult = await udpClient.ReceiveAsync(_life.Token);
              var msgBytes = udpResult.Buffer;

              var decryptedBytes = rsaAes.Decrypt(msgBytes).ToArray();

              if (!GenericUdpMsg.TryGetFromByteArray(decryptedBytes, out var msg))
                continue;

              if (msg.Type != StoreLocationUdpMsg.Type)
                continue;

              if (!StoreLocationUdpMsg.TryGetFromByteArray(msg.Payload[..msg.PayloadSize], out var req))
                continue;

              _log.Info($"Got udp req: '{req.RoomId}/{req.Username}' -> {req.Lat};{req.Lng}");

              try
              {
                await _roomsController.SaveNewPathPointAsync(
                  _log[$"{udpResult.RemoteEndPoint}"],
                  udpResult.RemoteEndPoint.Address,
                  req.RoomId,
                  req.Username,
                  req.SessionId,
                  req.WipeOldPath,
                  req.Lat,
                  req.Lng,
                  req.Alt,
                  req.Acc == float.MinValue ? null : req.Acc,
                  req.Speed == float.MinValue ? null : req.Speed,
                  req.Battery == float.MinValue ? null : req.Battery,
                  req.GsmSignal == float.MinValue ? null : req.GsmSignal,
                  req.Bearing == float.MinValue ? null : req.Bearing,
                  _life.Token);
              }
              catch (Exception ex)
              {
                _log.Error($"Error occured while trying to save path point: {ex}");
              }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
              _log.Error($"Error occured while trying to process udp msg: {ex}");
            }
          }
        });

        thread.IsBackground = true;
        thread.Start();
      });
  }


}
