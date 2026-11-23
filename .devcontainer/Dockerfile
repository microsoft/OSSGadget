# The base image ships a Yarn apt source with an expired signing key, which makes `apt-get update`
# fail (exit 100) and breaks every dev container feature that installs packages.
FROM mcr.microsoft.com/devcontainers/dotnet:1-8.0-bookworm
RUN rm -f /etc/apt/sources.list.d/yarn.list
