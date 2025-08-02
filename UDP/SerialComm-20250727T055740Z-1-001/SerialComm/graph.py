import pandas as pd
import matplotlib.pyplot as plt

# Load the CSV file
file_path = 'recorded_weights.csv'
data = pd.read_csv(file_path)

# Plot the graph
plt.figure(figsize=(10, 6))
plt.plot(data['Elapsed_Time'], data['Volume (ml)'], label='Weight (g)', color='blue')
plt.xlabel('Elapsed Time (seconds)')
plt.ylabel('Weight (g)')
plt.title('Weight vs Elapsed Time')
plt.grid(True)
plt.legend()
plt.show()