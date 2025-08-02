import serial
import socket
import time
import csv

# Serial port settings (adjust COM port and baudrate)
SERIAL_PORT = 'COM3'
BAUDRATE = 9600

# UDP settings (Unity listens here)
UDP_IP = '127.0.0.1'  # Replace with your Unity PC IP
UDP_PORT = 5005

# CSV file settings
CSV_FILE = 'recorded_weights.csv'

# Open UDP socket
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

# Open serial port
ser = serial.Serial(SERIAL_PORT, BAUDRATE, timeout=1)

# Wait a bit for the serial port to initialize (optional but recommended)
time.sleep(2)

# Automatically send 's' to start reading from the scale
ser.write(b's\n')  # sending 's' plus newline (adjust if scale expects just 's')

reading_active = True  # since we sent 's', start reading automatically

MAX_WEIGHT = 0.55
timestamps = []
weights = []
start_time = time.time()

# Open CSV file and write header
with open(CSV_FILE, mode='w', newline='') as file:
    writer = csv.writer(file)
    writer.writerow(['Elapsed_Time', 'Volume (ml)'])  # Updated header row

    def send_udp_weight(weight):
        normalized_level = weight / MAX_WEIGHT
        # Clamp normalized_level between 0 and 1
        normalized_level = max(0.0, min(normalized_level, 1.0))
        message = str(normalized_level).encode('utf-8')
        sock.sendto(message, (UDP_IP, UDP_PORT))
        print(f"Sent normalized level via UDP: {normalized_level}")

    try:
        print("Listening on serial...")
        while True:
            if ser.in_waiting:
                line = ser.readline().decode('utf-8', errors='ignore').strip()
                if not line:
                    continue

                # Optional: still handle manual start/stop commands if needed
                if line == 's':
                    reading_active = True
                    print("Started reading weight.")
                    continue
                elif line == 'e':
                    reading_active = False
                    print("Stopped reading weight.")
                    continue

                if reading_active:
                    # Example line: 00:00:11.720;83;0.312;
                    if line.endswith(';'):
                        line = line[:-1]

                    parts = line.split(';')
                    if len(parts) >= 3:
                        weight_str = parts[-1]  # last attribute is weight
                        try:
                            weight = float(weight_str) * 1000  # Multiply by 1000 for ml
                            send_udp_weight(weight / 1000)  # Send normalized weight in kg
                            # Add these lines to record and plot
                            current_time = time.time() - start_time
                            timestamps.append(current_time)
                            weights.append(weight)
                            # Write to CSV
                            writer.writerow([current_time, weight])
                        except ValueError:
                            print(f"Invalid weight value: {weight_str}")
    except KeyboardInterrupt:
        print("Exiting...")