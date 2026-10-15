# CVD Inbox runtime image. It holds the same linux-x64 publish output as the release zip:
# run scripts/publish.sh, which builds the client and fills artifacts/publish first.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble-chiseled@sha256:84def277556c2843a44fdeefbd3145d0a2e32d8f736d0c1a7b0a96b2880b0187

# No debugger, profiler or EventPipe endpoints. Data and keys go to the volume on /home/app.
# TMPDIR points there too: with --read-only /tmp is not writable, and the key store writes a temp file first.
ENV DOTNET_EnableDiagnostics=0 \
    Inbox__DataPath=/home/app/data \
    TMPDIR=/home/app

WORKDIR /app
COPY artifacts/publish/ ./

# The chiseled image has no shell; "app" is a non-root user (UID 1654).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["./cvd-inbox"]
