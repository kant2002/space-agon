#!/bin/bash

# Copyright 2022 Google LLC All Rights Reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

# DO NOT RUN this script directly.
# Run `make skaffold-setup` command after creating the cluster.
# This script setup skaffold.yaml

PROJECT_ID=$1
REGISTRY=$2

gcloud config set project ${PROJECT_ID}
gcloud services enable cloudbuild.googleapis.com

gcloud projects add-iam-policy-binding ${PROJECT_ID} \
    --member=serviceAccount:$(gcloud projects describe ${PROJECT_ID} \
    --format="value(projectNumber)")@cloudbuild.gserviceaccount.com \
    --role="roles/storage.objectViewer"

ESC_REGISTRY=$(echo ${REGISTRY} | sed -e 's/\\/\\\\/g; s/\//\\\//g; s/&/\\\&/g') &&
sed -E 's/image: (.*)\/([^\/]*)/image: '${ESC_REGISTRY}'\/\2/;s/PROJECTID/'${PROJECT_ID}'/g' skaffold_template.yaml > skaffold.yaml

echo "You are ready to run skaffold commands"