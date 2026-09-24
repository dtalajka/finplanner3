pipeline {
    agent any

    parameters {
        string(
            name: 'ZOT_REGISTRY',
            defaultValue: 'zot.infra.svc.cluster.local:5000',
            description: 'Zot registry host and optional port, without https://'
        )
        string(
            name: 'IMAGE_REPOSITORY',
            defaultValue: 'finplanner',
            description: 'Repository path for the image in Zot'
        )
        string(
            name: 'IMAGE_TAG',
            defaultValue: '',
            description: 'Image tag; defaults to APP_VERSION-build-JENKINS_BUILD_NUMBER'
        )
        string(
            name: 'APP_VERSION',
            defaultValue: '0.1.0',
            description: 'Application version, preferably matching the Git release tag'
        )
    }

    options {
        disableConcurrentBuilds()
    }

    environment {
        DOCKER_BUILDKIT = '1'
        BUILDKIT_PROGRESS = 'plain'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Pipeline Source Diagnostics') {
            steps {
                sh '''
                    set -eu
                    echo "Git commit: $(git rev-parse HEAD)"
                    echo "Git branch: $(git branch --show-current || true)"
                    echo "Jenkinsfile SHA256: $(sha256sum Jenkinsfile | awk '{print $1}')"
                    echo "Jenkinsfile first line: $(head -n 1 Jenkinsfile)"
                '''
            }
        }

        stage('Docker Diagnostics') {
            steps {
                sh '''
                    set +e
                    echo "DOCKER_HOST=$DOCKER_HOST"
                    echo "DOCKER_TLS_VERIFY=$DOCKER_TLS_VERIFY"
                    echo "DOCKER_CERT_PATH=$DOCKER_CERT_PATH"
                    docker context show
                    docker version
                    docker info
                '''
            }
        }

        stage('Test') {
            steps {
                // .NET build + tests run inside the Docker build, so the agent needs no .NET/Node SDK.
                sh 'docker build --target test --tag "finplanner-test:${BUILD_NUMBER}" .'
            }
            post {
                always {
                    sh '''
                        set +e
                        rm -rf test-results && mkdir -p test-results
                        cid=$(docker create "finplanner-test:${BUILD_NUMBER}" 2>/dev/null)
                        if [ -n "$cid" ]; then
                            docker cp "$cid:/testresults/." test-results/
                            docker rm "$cid"
                        fi
                        docker image rm "finplanner-test:${BUILD_NUMBER}" 2>/dev/null
                        true
                    '''
                    archiveArtifacts artifacts: 'test-results/*.trx', allowEmptyArchive: true
                }
            }
        }

        stage('Build and Push') {
            steps {
                script {
                    def image = "${params.ZOT_REGISTRY.trim()}/${params.IMAGE_REPOSITORY.trim()}"
                    def appVersion = params.APP_VERSION.trim()
                    def vcsRef = sh(
                        script: 'git rev-parse HEAD',
                        returnStdout: true
                    ).trim()
                    def imageTag = params.IMAGE_TAG?.trim()
                    if (!imageTag) {
                        imageTag = "${appVersion}-build-${env.BUILD_NUMBER}"
                    }
                    def buildDate = sh(
                        script: 'date -u +%Y-%m-%dT%H:%M:%SZ',
                        returnStdout: true
                    ).trim()

                    echo "Building ${image}:${imageTag}"
                    echo "Application version: ${appVersion}"
                    echo "Jenkins build number: ${env.BUILD_NUMBER}"
                    echo "Git commit: ${vcsRef}"

                    // Frontend (lint + build) and the EF migrations bundle are built in this image build too.
                    sh """
                        docker build \
                            --target runtime \
                            --build-arg APP_VERSION='${appVersion}' \
                            --build-arg BUILD_NUMBER='${env.BUILD_NUMBER}' \
                            --build-arg VCS_REF='${vcsRef}' \
                            --build-arg BUILD_DATE='${buildDate}' \
                            --tag '${image}:${imageTag}' .
                    """
                    sh "docker push '${image}:${imageTag}'"
                }
            }
        }
    }

    post {
        always {
            sh 'docker image prune --force || true'
        }
    }
}
