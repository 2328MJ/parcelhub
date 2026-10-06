# Local Jenkins

Jenkins in a Docker container, with the .NET 8 SDK already installed.

## Start it

Needs Rancher Desktop (or Docker Desktop) running. If `docker` isn't found, open a new terminal so it
picks up the updated PATH.

```powershell
cd jenkins
docker compose up -d --build        # first build takes a few minutes
```

Open http://localhost:8080. Get the one-time unlock password with:

```powershell
docker exec parcelhub-jenkins cat /var/jenkins_home/secrets/initialAdminPassword
```

(In Git Bash, put `MSYS_NO_PATHCONV=1` in front, or Git Bash rewrites the `/var/...` path.)

Then choose **Select plugins to install → None** (the useful ones are already baked in from
`plugins.txt`) and create your admin user.

## Check .NET works

1. **New Item → Pipeline**, call it `dotnet-check`.
2. Paste this as the script and click **Build Now**:

   ```groovy
   pipeline {
     agent any
     stages {
       stage('dotnet') { steps { sh 'dotnet --info' } }
     }
   }
   ```

The console output should show SDK 8.0.x.

## Day to day

| Task | Command |
|---|---|
| Stop | `docker compose stop` |
| Start again | `docker compose start` |
| Logs | `docker logs -f parcelhub-jenkins` |
| Rebuild after editing Dockerfile/plugins | `docker compose up -d --build` |
| Wipe everything (jobs, users, plugins) | `docker compose down -v` |

Everything Jenkins knows lives in the `jenkins_home` volume, so stopping or rebuilding the
container keeps your jobs.

## Next: step 4

Push this repo to GitHub, add a `Jenkinsfile` and a shared library with
`standardDeliveryPipeline`, and point a **Multibranch Pipeline** job at the repo. GitHub can't reach
Jenkins on your laptop, so set the job to scan the repo every few minutes instead of using webhooks.
