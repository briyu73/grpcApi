import os
import subprocess

# External proto path
proto_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'Protos', 'greeter.proto')

# Generate
subprocess.run([
    'python', '-m', 'grpc_tools.protoc',
    '-I' + os.path.dirname(proto_path),
    '--python_out=.',
    '--grpc_python_out=.',
    proto_path
])

print("Generated greeter_pb2.py and greeter_pb2_grpc.py")