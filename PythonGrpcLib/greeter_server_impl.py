import grpc
from concurrent import futures
import time

import greeter_pb2
import greeter_pb2_grpc

class GreeterServiceImpl(greeter_pb2_grpc.GreeterServicer):

    def SayHello(self, request, context):
        return greeter_pb2.HelloReply(message='Hello %s!' % request.name)

def serve(address='0.0.0.0:50051'):
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    greeter_pb2_grpc.add_GreeterServicer_to_server(GreeterServiceImpl(), server)
    server.add_insecure_port(address)
    server.start()
    print(f"Server listening on {address}")
    try:
        while True:
            time.sleep(86400)
    except KeyboardInterrupt:
        server.stop(0)