#!/bin/bash
exec > /var/log/user-data.log 2>&1
set -x
dnf install -y git docker fontconfig java-21-amazon-corretto-headless
systemctl enable --now docker
curl -fsSL -o /etc/yum.repos.d/jenkins.repo https://pkg.jenkins.io/redhat-stable/jenkins.repo
rpm --import https://pkg.jenkins.io/redhat-stable/jenkins.io-2023.key
dnf install -y jenkins
usermod -aG docker jenkins
KVER=$(curl -fsSL https://dl.k8s.io/release/stable.txt)
curl -fsSL -o /usr/local/bin/kubectl "https://dl.k8s.io/release/$KVER/bin/linux/amd64/kubectl"
chmod +x /usr/local/bin/kubectl
systemctl enable --now jenkins
