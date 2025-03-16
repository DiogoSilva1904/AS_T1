#!/bin/bash

# Endpoint URL
URL="http://localhost:5222/api/catalog/items/99?api-version=1.0"

# Number of requests to send
REQUESTS=100

# Loop to send multiple requests
for ((i=1; i<=REQUESTS; i++))
do
  echo "Sending request $i"
  curl -k $URL
done

# Wait for all background processes to finish
wait
echo "All requests completed."
