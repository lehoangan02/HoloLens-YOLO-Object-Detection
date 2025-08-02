import pandas as pd

def timestamp_to_seconds(ts):
    """Convert MM:SS.s format to seconds."""
    try:
        minutes, seconds = ts.split(":")
        return int(minutes) * 60 + float(seconds)
    except:
        return None  # Handle malformed timestamps gracefully

# Load the data
input_file = "scale_data_rice.csv"
output_file = "scale_data_rice_converted.csv"
df = pd.read_csv(input_file)

# Remove rows with duplicate header or malformed data
df = df[df['Timestamp'] != 'Timestamp']
df = df[df['Timestamp'].str.count(":") == 1]

# Convert Timestamp to Seconds
df['Seconds'] = df['Timestamp'].apply(timestamp_to_seconds)

# Drop rows where conversion failed (optional)
df = df.dropna(subset=['Seconds'])

# Save to new CSV
df.to_csv(output_file, index=False)

print(f"Converted file saved as: {output_file}")
