aws ecr-public get-login-password --region us-east-1 | docker login --username AWS --password-stdin public.ecr.aws/m4u6b5p7
docker build . -t prototest
docker tag prototest:latest public.ecr.aws/m4u6b5p7/prototest:latest
docker push public.ecr.aws/m4u6b5p7/prototest:latest