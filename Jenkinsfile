pipeline {
  agent any
  options {
    timestamps()
    disableConcurrentBuilds()
  }
  environment {
    AWS_REGION = 'ap-southeast-1'
    REGISTRY   = '084250374652.dkr.ecr.ap-southeast-1.amazonaws.com'
    CLUSTER    = 'gymnote-eks'
  }
  stages {
    stage('Prepare') {
      steps {
        script { env.TAG = sh(returnStdout: true, script: 'git rev-parse --short HEAD').trim() }
        echo "Image tag: ${env.TAG}"
      }
    }
    stage('Test') {
      parallel {
        stage('Backend tests') {
          steps {
            sh '''docker run --rm --user "$(id -u):$(id -g)" -e HOME=/tmp -e DOTNET_NOLOGO=1 \
                  -v "$PWD/app/backend":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test'''
          }
        }
        stage('Frontend tests') {
          steps {
            sh '''docker run --rm --user "$(id -u):$(id -g)" -e HOME=/tmp \
                  -v "$PWD/app/frontend":/src -w /src node:24-alpine \
                  sh -c "npm ci && npm run typecheck && npm test"'''
          }
        }
      }
    }
    stage('Build images') {
      steps {
        sh '''docker build -t $REGISTRY/gymnote-backend:$TAG ./app/backend
              docker build -t $REGISTRY/gymnote-frontend:$TAG ./app/frontend'''
      }
    }
    stage('Push to ECR') {
      steps {
        sh '''aws ecr get-login-password --region $AWS_REGION | docker login --username AWS --password-stdin $REGISTRY
              docker push $REGISTRY/gymnote-backend:$TAG
              docker push $REGISTRY/gymnote-frontend:$TAG'''
      }
    }
    stage('Connect to cluster') {
      steps {
        sh 'aws eks update-kubeconfig --region $AWS_REGION --name $CLUSTER'
      }
    }
    stage('Deploy dev') {
      steps {
        sh './scripts/deploy.sh dev $TAG'
        sh './scripts/smoke.sh dev'
      }
    }
    stage('Deploy staging') {
      steps {
        sh './scripts/deploy.sh staging $TAG'
        sh './scripts/smoke.sh staging'
      }
    }
    stage('Approve prod') {
      steps {
        timeout(time: 30, unit: 'MINUTES') {
          input message: "Deploy ${env.TAG} to PROD?", ok: 'Deploy'
        }
      }
    }
    stage('Deploy prod') {
      steps {
        sh './scripts/deploy.sh prod $TAG'
        sh './scripts/smoke.sh prod'
      }
    }
  }
  post {
    always {
      sh 'docker image prune -f || true'
    }
  }
}
