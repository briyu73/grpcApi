import threading
import time
import grpc

import greeter_pb2
import greeter_pb2_grpc

from PythonGrpcLib.greeter_server_impl import serve  # Uses library

def run_server():
    serve()

def run_client():
    time.sleep(1)  # Wait for server
    channel = grpc.insecure_channel('localhost:50051')
    stub = greeter_pb2_grpc.GreeterStub(channel)
    response = stub.SayHello(greeter_pb2.HelloRequest(name='World'))
    print(f"Client received: {response.message}")
    channel.close()

if __name__ == '__main__':
    server_thread = threading.Thread(target=run_server)
    server_thread.daemon = True
    server_thread.start()

    run_client()