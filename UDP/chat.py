import socket
import threading

# Config
MY_IP = '0.0.0.0'              # Bind to all interfaces
MY_PORT = 5007
UNITY_IP = '10.0.10.203'      # Replace with Unity machine IP
UNITY_PORT = 5006

# Setup
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind((MY_IP, MY_PORT))

# Receive thread
def receive():
    while True:
        data, addr = sock.recvfrom(1024)
        print(f"Received from Unity: {data.decode()}")

# Send to Unity
def send_to_unity(msg):
    sock.sendto(msg.encode(), (UNITY_IP, UNITY_PORT))

# Run receiver
threading.Thread(target=receive, daemon=True).start()

# Example interaction
while True:
    msg = input("Send to Unity: ")
    send_to_unity(msg)

