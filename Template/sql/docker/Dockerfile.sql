FROM mcr.microsoft.com/mssql/server:2019-latest

RUN mkdir -p /var/opt/mssql/

USER root

COPY ./sql /var/opt/mssql/
RUN chmod +x /var/opt/mssql/entrypoint.sh
RUN chmod +x /var/opt/mssql/run-initialization.sh

WORKDIR /var/opt/mssql

# Copy initialization scripts
# COPY . /usr/src/app

RUN apt-get update && apt-get install -y iputils-ping

# Grant permissions for the run-initialization script to be executable
# RUN chmod +x /usr/src/app/run-initialization.sh

# Set environment variables, not to have to write them with docker run command
# Note: make sure that your password matches what is in the run-initialization script 
ENV SA_PASSWORD=Dock2Gently
ENV ACCEPT_EULA=Y
ENV MSSQL_PID=Developer

# Expose port 1433 in case accesing from other container
EXPOSE 1433

# Run Microsoft SQl Server and initialization script (at the same time)
# Note: If you want to start MsSQL only (without initialization script) you can comment below line out, CMD entry from base image will be taken
CMD /bin/bash ./entrypoint.sh