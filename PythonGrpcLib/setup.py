from setuptools import setup, find_packages

setup(
    name='python_grpc_lib',
    version='0.1',
    packages=find_packages(),
    install_requires=['grpcio', 'grpcio-tools'],
)