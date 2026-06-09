#!/bin/bash

set -e

# ============================================================
# SCRIPT DE INFRAESTRUTURA AZURE CLI
# Projeto: Argus Operations .NET
# RM: 561052
# Deploy: ACR + ACI
# ============================================================

# Variaveis principais
RM="rm561052"

RESOURCE_GROUP_LOCATION="eastus"
ACR_LOCATION="eastus"
ACI_LOCATION="eastus"

RESOURCE_GROUP="rg-argus-${RM}"
ACR_NAME="acrargus${RM}"
ACI_NAME="aci-argus-${RM}"
DNS_LABEL="argus-${RM}-api"
IMAGE_NAME="argus-operations-api"
CONTAINER_PORT="8080"

echo "============================================================"
echo "Criando infraestrutura Azure para o projeto Argus Operations"
echo "Resource Group: ${RESOURCE_GROUP}"
echo "Resource Group Location: ${RESOURCE_GROUP_LOCATION}"
echo "ACR: ${ACR_NAME}"
echo "ACR Location: ${ACR_LOCATION}"
echo "ACI: ${ACI_NAME}"
echo "ACI Location: ${ACI_LOCATION}"
echo "DNS Label: ${DNS_LABEL}"
echo "============================================================"

# Verificar se Azure CLI esta instalado
if ! command -v az &> /dev/null
then
  echo "ERRO: Azure CLI nao encontrado."
  echo "Instale o Azure CLI no computador ou execute este script pelo Azure Cloud Shell."
  exit 1
fi

# Login na Azure
echo "Verificando login na Azure..."
if ! az account show > /dev/null 2>&1
then
  echo "Voce nao esta logado na Azure. Executando az login..."
  az login
fi

# Mostrar assinatura atual
echo "Assinatura Azure em uso:"
az account show --query "{name:name, user:user.name}" -o table

# Registrar providers necessarios
echo "Registrando providers necessarios..."
az provider register --namespace Microsoft.ContainerRegistry
az provider register --namespace Microsoft.ContainerInstance

echo "Aguardando registro do provider Microsoft.ContainerRegistry..."
while [ "$(az provider show --namespace Microsoft.ContainerRegistry --query registrationState -o tsv)" != "Registered" ]; do
  echo "Aguardando Microsoft.ContainerRegistry..."
  sleep 10
done

echo "Aguardando registro do provider Microsoft.ContainerInstance..."
while [ "$(az provider show --namespace Microsoft.ContainerInstance --query registrationState -o tsv)" != "Registered" ]; do
  echo "Aguardando Microsoft.ContainerInstance..."
  sleep 10
done

# Criar ou atualizar Resource Group
echo "Criando ou atualizando Resource Group..."
az group create \
  --name "${RESOURCE_GROUP}" \
  --location "${RESOURCE_GROUP_LOCATION}"

# Criar Azure Container Registry se nao existir
echo "Verificando Azure Container Registry..."
if az acr show --name "${ACR_NAME}" --resource-group "${RESOURCE_GROUP}" > /dev/null 2>&1
then
  echo "Azure Container Registry ja existe: ${ACR_NAME}"
else
  echo "Criando Azure Container Registry..."
  az acr create \
    --resource-group "${RESOURCE_GROUP}" \
    --name "${ACR_NAME}" \
    --location "${ACR_LOCATION}" \
    --sku Basic \
    --admin-enabled true
fi

# Criar Azure Container Instance inicial com imagem publica temporaria
# A pipeline de release depois substituira essa imagem pela imagem real da API.
echo "Verificando Azure Container Instance..."
if az container show --name "${ACI_NAME}" --resource-group "${RESOURCE_GROUP}" > /dev/null 2>&1
then
  echo "Azure Container Instance ja existe: ${ACI_NAME}"
else
  echo "Criando Azure Container Instance com imagem temporaria..."
  az container create \
    --resource-group "${RESOURCE_GROUP}" \
    --name "${ACI_NAME}" \
    --location "${ACI_LOCATION}" \
    --image "mcr.microsoft.com/dotnet/samples:aspnetapp" \
    --os-type Linux \
    --cpu 1 \
    --memory 1 \
    --ports "${CONTAINER_PORT}" \
    --ip-address Public \
    --dns-name-label "${DNS_LABEL}" \
    --environment-variables \
      ASPNETCORE_ENVIRONMENT="Production" \
      ASPNETCORE_URLS="http://+:8080"
fi

# Mostrar recursos criados
echo "Listando recursos criados no Resource Group..."
az resource list \
  --resource-group "${RESOURCE_GROUP}" \
  --query "[].{Nome:name, Tipo:type, Localizacao:location}" \
  -o table

# Mostrar URL publica do ACI
ACI_FQDN=$(az container show \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${ACI_NAME}" \
  --query "ipAddress.fqdn" \
  -o tsv)

echo "============================================================"
echo "Infraestrutura criada com sucesso!"
echo "Resource Group: ${RESOURCE_GROUP}"
echo "Resource Group Location: ${RESOURCE_GROUP_LOCATION}"
echo "ACR: ${ACR_NAME}.azurecr.io"
echo "ACR Location: ${ACR_LOCATION}"
echo "ACI: ${ACI_NAME}"
echo "ACI Location: ${ACI_LOCATION}"
echo "URL publica inicial: http://${ACI_FQDN}:${CONTAINER_PORT}"
echo "Imagem inicial temporaria: mcr.microsoft.com/dotnet/samples:aspnetapp"
echo "A pipeline de Release ira publicar a imagem real da API no ACI."
echo "============================================================"