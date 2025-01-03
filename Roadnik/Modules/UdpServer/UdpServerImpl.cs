﻿using Ax.Fw.Crypto;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Common.ReqRes.Udp;
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
      ILog _log) => new UdpServerImpl(_settingsController, _lifetime, _log["udp-server"]));
  }

  private UdpServerImpl(
    ISettingsController _settingsController,
    IReadOnlyLifetime _lifetime,
    ILog _log)
  {
    var confScheduler = new EventLoopScheduler();

    _settingsController.Settings
      .DistinctUntilChanged(_ => HashCode.Combine(_?.IpBind, _?.PortBind, _?.UdpTransportPublicKeyPath, _?.UdpTransportPrivateKeyPath, _?.UdpTransportPrivateKeyPassphrase))
      .HotAlive(_lifetime, confScheduler, (_conf, _life) =>
      {
        if (_conf == null)
          return;

        if (!File.Exists(_conf.UdpTransportPrivateKeyPath))
        {
          _log.Warn($"Private key is not exist - udp server is inactive");
          return;
        }

        var privateKey = File.ReadAllText(_conf.UdpTransportPrivateKeyPath);
        var rsaAes = _life.ToDisposeOnEnded(new RsaAesGcm(null, privateKey, _conf.UdpTransportPrivateKeyPassphrase));

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

              if (!StoreLocationUdpMsg.TryGetFromByteArray(msg.Payload[..msg.PayloadSize], out var storeReq))
                continue;

              _log.Info($"Got udp req: '{storeReq.RoomId}/{storeReq.Username}' -> {storeReq.Lat};{storeReq.Lng}");
              await udpClient.SendAsync(new byte[] { 0, 1, 2, 3, 4, 5 }, udpResult.RemoteEndPoint, _life.Token);
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
