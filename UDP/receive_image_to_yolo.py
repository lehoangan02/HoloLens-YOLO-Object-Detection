import socket
import numpy as np
import cv2
from ultralytics import YOLO
import torch

# IP and port to listen on
LISTEN_IP = "0.0.0.0"   # Listen on all interfaces
LISTEN_PORT = 5010      # Must match sender’s port

# Check if MPS is available and set the device
device = "mps" if torch.backends.mps.is_available() else "cpu"
print(f"Using device: {device}")

model = YOLO("yolov8n.pt")  # Load the YOLO model
model.to(device)  # Move the model to the specified device

def main():
    # Create and bind a UDP socket
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind((LISTEN_IP, LISTEN_PORT))
    sock.settimeout(0.01)  # short timeout so we can update the window even if a packet is late

    # Prepare the window
    cv2.namedWindow("Received Frame", cv2.WINDOW_NORMAL)
    cv2.startWindowThread()

    frame = None
    try:
        while True:
            # 1. Try to receive one JPEG packet (non-blocking-ish)
            try:
                data, _ = sock.recvfrom(200_000)
                arr = np.frombuffer(data, np.uint8)
                decoded = cv2.imdecode(arr, cv2.IMREAD_COLOR)
                if decoded is not None:
                    frame = decoded
            except socket.timeout:
                pass

            # 2. Always display the latest frame
            if frame is not None:
                results = model(frame)                # Run inference
                print(results[0].boxes)               # Print detection results
                annotated_frame = results[0].plot()   # Draw detections
                cv2.imshow("Received Frame", annotated_frame)
                
                # cv2.imshow("Received Frame", frame)

            # 3. Pump the GUI event loop (no key checks)
            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

    except KeyboardInterrupt:
        pass
    finally:
        sock.close()
        cv2.destroyAllWindows()

if __name__ == "__main__":
    main()
