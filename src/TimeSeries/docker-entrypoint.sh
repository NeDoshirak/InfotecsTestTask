#!/bin/sh
set -e

/app/efbundle --connection "$ConnectionStrings__Postgres"
exec dotnet /app/TimeSeries.dll
