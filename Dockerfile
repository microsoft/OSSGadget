FROM mcr.microsoft.com/dotnet/sdk:5.0
COPY . /app/
WORKDIR /app/src
RUN set -o errexit -o nounset \
    && dotnet build \
    && ln --symbolic /app/src/oss-characteristics/bin/Debug/net5.0/oss-characteristic /usr/bin/oss-characteristic \
    && ln --symbolic /app/src/oss-defog/bin/Debug/net5.0/oss-defog /usr/bin/oss-defog \
    && ln --symbolic /app/src/oss-detect-backdoor/bin/Debug/net5.0/oss-detect-backdoor /usr/bin/oss-detect-backdoor \
    && ln --symbolic /app/src/oss-detect-cryptography/bin/Debug/net5.0/oss-detect-cryptography /usr/bin/oss-detect-cryptography \
    && ln --symbolic /app/src/oss-diff/bin/Debug/net5.0/oss-diff /usr/bin/oss-diff \
    && ln --symbolic /app/src/oss-download/bin/Debug/net5.0/oss-download /usr/bin/oss-download \
    && ln --symbolic /app/src/oss-find-domain-squats/bin/Debug/net5.0/oss-find-domain-squats /usr/bin/oss-find-domain-squats \
    && ln --symbolic /app/src/oss-find-source/bin/Debug/net5.0/oss-find-source /usr/bin/oss-find-source \
    && ln --symbolic /app/src/oss-find-squats/bin/Debug/net5.0/oss-find-squats /usr/bin/oss-find-squats \
    && ln --symbolic /app/src/oss-health/bin/Debug/net5.0/oss-health /usr/bin/oss-health \
    && ln --symbolic /app/src/oss-metadata/bin/Debug/net5.0/oss-metadata /usr/bin/oss-metadata \
    && ln --symbolic /app/src/oss-risk-calculator/bin/Debug/net5.0/oss-risk-calculator /usr/bin/oss-risk-calculator
# home directory of root user
WORKDIR /root
