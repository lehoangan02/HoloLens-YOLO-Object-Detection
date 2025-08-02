import serial
import csv
import time

SERIAL_PORT = 'COM3'      # Your actual COM port
BAUD_RATE = 9600
CSV_FILE = 'scale_data_rice.csv'

# Open serial connection
ser = serial.Serial(SERIAL_PORT, BAUD_RATE, timeout=1)
time.sleep(2)  # Wait for Arduino to reset

# Send 's' to start recording
ser.write(b's\n')  # or b's\r\n' if Arduino expects CR+LF
print("Sent 's' to Arduino to start recording.")

# Open CSV file
with open(CSV_FILE, mode='a', newline='') as file:
    writer = csv.writer(file)
    writer.writerow(['Timestamp', 'ID', 'Weight'])
    print("Recording scale data. Press Ctrl+C to stop.")

    try:
        while True:
            line = ser.readline().decode('utf-8').strip()
            print(line)

            # Remove trailing semicolon and split
            parts = line.strip().strip(';').split(';')

            # Debug print
            print(parts)

            if len(parts) == 3:
                writer.writerow(parts)  # Writes as separate CSV columns

    except KeyboardInterrupt:
        print("Recording stopped by user.")

ser.close()
