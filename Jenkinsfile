pipeline {
  agent { label 'smarthotel-linux-docker' }

  options {
    timestamps()
    disableConcurrentBuilds()
    timeout(time: 60, unit: 'MINUTES')
  }

  parameters {
    booleanParam(name: 'DEPLOY_STAGING', defaultValue: false, description: 'Deploy tested commit images to staging')
  }

  environment {
    AWS_REGION = 'ap-south-1'
    ECR_REGISTRY = credentials('smarthotel-ecr-registry')
    KUBECONFIG = credentials('smarthotel-staging-kubeconfig')
    IMAGE_TAG = "${env.GIT_COMMIT}"
  }

  stages {
    stage('Checkout') {
      steps { checkout scm }
    }

    stage('Backend tests') {
      parallel {
        stage('Identity') { steps { dir('services/identity-service/SmartHotel.Identity') { sh 'dotnet test SmartHotel.Identity.sln -c Release --nologo' } } }
        stage('Booking') { steps { dir('services/booking-payments-service/SmartHotel.Booking') { sh 'dotnet test SmartHotel.Booking.sln -c Release --nologo' } } }
        stage('Hotel Ops') { steps { dir('services/hotel-ops-service/SmartHotel.HotelOps') { sh 'dotnet test SmartHotel.HotelOps.sln -c Release --nologo' } } }
        stage('Notifications') { steps { dir('services/notification-service/SmartHotel.Notifications') { sh 'dotnet test -c Release --nologo' } } }
        stage('Field Ops') { steps { dir('services/field-ops-service/smarthotel-fieldops') { sh 'mvn -B test' } } }
        stage('Gateway') { steps { sh 'dotnet test tests/SmartHotel.Gateway.IntegrationTests/SmartHotel.Gateway.IntegrationTests.csproj -c Release --nologo' } }
      }
    }

    stage('Frontend') {
      steps {
        dir('frontend') {
          sh 'npm ci'
          sh 'npm run lint'
          sh 'npm run build'
        }
      }
    }

    stage('Security and IaC checks') {
      parallel {
        stage('Dependency audit') {
          steps {
            dir('frontend') { sh 'npm audit --audit-level=critical' }
            sh 'dotnet list services/identity-service/SmartHotel.Identity/SmartHotel.Identity.sln package --vulnerable --include-transitive'
          }
        }
        stage('Terraform') {
          steps {
            sh 'terraform fmt -check -recursive infra/terraform'
            dir('infra/terraform/environments/staging') {
              sh 'terraform init -backend=false'
              sh 'terraform validate'
            }
          }
        }
        stage('Kubernetes render') {
          steps { sh 'kubectl kustomize infra/kubernetes/overlays/staging > /tmp/smarthotel-staging.yaml' }
        }
      }
    }

    stage('Build images') {
      steps {
        sh 'ci/build-images.sh "$ECR_REGISTRY" "$IMAGE_TAG"'
      }
    }

    stage('Scan images') {
      steps { sh 'ci/scan-images.sh "$ECR_REGISTRY" "$IMAGE_TAG"' }
    }

    stage('Push to ECR') {
      steps {
        sh 'aws ecr get-login-password --region "$AWS_REGION" | docker login --username AWS --password-stdin "$ECR_REGISTRY"'
        sh 'ci/push-images.sh "$ECR_REGISTRY" "$IMAGE_TAG"'
      }
    }

    stage('Deploy staging') {
      when { expression { params.DEPLOY_STAGING } }
      steps {
        input message: "Deploy commit ${env.GIT_COMMIT} to staging?", ok: 'Deploy staging'
        sh 'ci/deploy-staging.sh "$ECR_REGISTRY" "$IMAGE_TAG"'
      }
    }

    stage('Staging smoke test') {
      when { expression { params.DEPLOY_STAGING } }
      steps {
        sh 'kubectl -n smarthotel-staging rollout status deployment/gateway --timeout=5m'
        sh 'kubectl -n smarthotel-staging get pods,svc,ingress'
      }
    }

    stage('Production approval boundary') {
      steps {
        echo 'Production deployment is intentionally not implemented as an automatic stage. Use a separately approved production job and change record.'
      }
    }
  }

  post {
    always { cleanWs(deleteDirs: true, disableDeferredWipeout: true) }
  }
}
