import socket
import numpy as np
import cv2

# IP and port to listen on
LISTEN_IP = "0.0.0.0"  # Listen on all available interfaces
LISTEN_PORT = 5010     # Match the sender's target port

def main():
    # Create a UDP socket
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind((LISTEN_IP, LISTEN_PORT))
    print(f"Listening for UDP packets on {LISTEN_IP}:{LISTEN_PORT}...")

    # Buffer to store incoming chunks
    image_data = bytearray()

    while True:
        try:
            # Receive data from the sender
            data, addr = sock.recvfrom(65535)  # Maximum UDP packet size
            print(f"Received {len(data)} bytes from {addr}")

            # Append the received chunk to the buffer
            image_data.extend(data)

            # Check if the buffer contains enough data for the full image
            width, height = 640, 640
            expected_size = width * height * 4  # RGBA format
            if len(image_data) >= expected_size:
                # Convert the byte array back to an image
                frame = np.frombuffer(image_data[:expected_size], dtype=np.uint8).reshape((height, width, 4))
                image_data = image_data[expected_size:]  # Remove processed data from the buffer

                # Display the image using OpenCV
                cv2.imshow("Received Frame", frame)
                cv2.waitKey(1)  # Add a small delay to allow the GUI thread to update
                if cv2.waitKey(1) & 0xFF == ord('q'):
                    break
        except Exception as e:
            print(f"Error reconstructing image: {e}")

    # Cleanup
    sock.close()
    cv2.destroyAllWindows()

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("Terminating...")
        exit()