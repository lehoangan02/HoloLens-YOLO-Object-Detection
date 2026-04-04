using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Listens for UDP commands from the Mac controller app and dispatches them
/// to EyeTracker / HeadTracker on the Unity main thread.
///
/// Supported commands (port 5015):
///   CMD:START_EYE   CMD:STOP_EYE   CMD:SEND_EYE
///   CMD:START_HEAD  CMD:STOP_HEAD  CMD:SEND_HEAD
///
/// Attach to any active GameObject. Assign eyeTracker and headTracker
/// in the Inspector.
/// </summary>
public class CommandReceiver : MonoBehaviour
{
    public EyeTracker  eyeTracker;
    public HeadTracker headTracker;

    private const int CmdPort = 5015;

    private UdpClient udpClient;
    private Thread    receiveThread;
    private bool      running;

    private readonly ConcurrentQueue<string> _cmdQueue =
        new ConcurrentQueue<string>();

    // ── Unity lifecycle ──────────────────────────────────────────────── //

    private void Start()
    {
        // Auto-find trackers if not assigned in Inspector
        if (eyeTracker  == null) eyeTracker  = FindObjectOfType<EyeTracker>();
        if (headTracker == null) headTracker = FindObjectOfType<HeadTracker>();

        if (eyeTracker  == null) Debug.LogWarning("[CommandReceiver] EyeTracker not found");
        if (headTracker == null) Debug.LogWarning("[CommandReceiver] HeadTracker not found");

        udpClient = new UdpClient(CmdPort);
        udpClient.Client.ReceiveTimeout = 1000; // so the thread can check `running`
        running = true;

        receiveThread = new Thread(ReceiveLoop)
            { IsBackground = true, Name = "CmdReceiver" };
        receiveThread.Start();

        Debug.Log($"[CommandReceiver] Listening on UDP port {CmdPort}");
    }

    private void Update()
    {
        // Drain command queue on the main thread (Unity API is single-threaded)
        while (_cmdQueue.TryDequeue(out string cmd))
            Dispatch(cmd);
    }

    private void OnDestroy()
    {
        running = false;
        udpClient?.Close();
    }

    // ── Background receive thread ────────────────────────────────────── //

    private void ReceiveLoop()
    {
        var ep = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref ep);
                string cmd  = Encoding.UTF8.GetString(data).Trim();
                Debug.Log($"[CommandReceiver] ← {cmd}  from {ep}");
                _cmdQueue.Enqueue(cmd);
            }
            catch (SocketException) { }  // ReceiveTimeout — expected, loop again
            catch (Exception e)
            {
                if (running)
                    Debug.LogError($"[CommandReceiver] Error: {e.Message}");
            }
        }
    }

    // ── Command dispatch (main thread) ───────────────────────────────── //

    private void Dispatch(string cmd)
    {
        switch (cmd)
        {
            case "CMD:START_EYE":   eyeTracker?.TurnOn();     break;
            case "CMD:STOP_EYE":    eyeTracker?.TurnOff();    break;
            case "CMD:SEND_EYE":    eyeTracker?.SendFile();   break;
            case "CMD:START_HEAD":  headTracker?.TurnOn();    break;
            case "CMD:STOP_HEAD":   headTracker?.TurnOff();   break;
            case "CMD:SEND_HEAD":   headTracker?.SendFile();  break;
            case "CMD:RESET":
                eyeTracker?.TurnOff();
                headTracker?.TurnOff();
                eyeTracker?.DeleteFile();
                headTracker?.DeleteFile();
                break;
            default:
                Debug.LogWarning($"[CommandReceiver] Unknown command: {cmd}");
                break;
        }
    }
}
