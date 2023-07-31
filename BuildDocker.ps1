docker build -f "CryptoBlade/Dockerfile" -t cryptoblade:latest .
docker save -o cryptoblade.tar cryptoblade:latest