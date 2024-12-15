import os
import torch
import OpenEXR
import Imath
import numpy as np
from tqdm import tqdm

def read_exr(file_path):
    exr_file = OpenEXR.InputFile(file_path)
    dw = exr_file.header()['dataWindow']
    width = dw.max.x - dw.min.x + 1
    height = dw.max.y - dw.min.y + 1
    
    # Extract relevant channels
    pixel_type = Imath.PixelType(Imath.PixelType.FLOAT)
    input_channels = [np.frombuffer(exr_file.channel(c, pixel_type), dtype=np.float32).reshape(height, width) for c in ['R', 'G']]
    target_channels = [np.frombuffer(exr_file.channel(c, pixel_type), dtype=np.float32).reshape(height, width) for c in ['B', 'A']]

    # Stack and convert to tensors
    input_tensor = torch.tensor(np.stack(input_channels, axis=0), dtype=torch.float32)  # Shape: (2, H, W)
    target_tensor = torch.tensor(np.stack(target_channels, axis=0), dtype=torch.float32)  # Shape: (2, H, W)

    return input_tensor, target_tensor

def preprocess_exr_files(input_dirs, output_dir, epsilon=0.01):
    os.makedirs(output_dir, exist_ok=True)

    for input_dir in input_dirs:
        files = sorted([f for f in os.listdir(input_dir) if f.endswith('.exr')])

        for f in tqdm(files, desc=f"Processing {input_dir}"):
            file_path = os.path.join(input_dir, f)
            input_tensor, target_tensor = read_exr(file_path)

            # Apply preprocessing steps
            target_tensor[0] -= epsilon  # Account for floating-point error
            target_tensor[0] = torch.max(target_tensor[0], torch.tensor(0.0))  # Ensure non-negative

            # Save as .pt file
            save_path = os.path.join(output_dir, f.replace('.exr', '.pt'))
            torch.save({'input': input_tensor, 'target': target_tensor}, save_path)


preprocess_exr_files(
    input_dirs=[os.getcwd() + '/data_noise_a/'],
    output_dir=os.getcwd() + "/processed_data_noise_a/"
)


"""preprocess_exr_files(
    input_dirs=[os.getcwd() + '/data_noise_a/', os.getcwd() + '/data_noise_b/', os.getcwd() + '/data_noise_c/', os.getcwd() + '/data_noise_d/', os.getcwd() + '/data_noise_e/'],
    output_dir=os.getcwd() + "/processed_data_noise_all/"
)

preprocess_exr_files(
    input_dirs=[os.getcwd() + '/data_lidar_a/'],
    output_dir=os.getcwd() + "/processed_data_lidar_all/"
)"""