#!/bin/bash

# =========================================
# PkLots - Inicialização do Projeto
# Fedora + Podman + .NET 8/10
# =========================================

echo "🚀 Inicializando PkLots..."

# # Criar estrutura de diretórios
# mkdir -p PkLots
# cd PkLots

# Estrutura raiz
mkdir -p src/{PkLots.Api,PkLots.Core,PkLots.Infrastructure,PkLots.Application}
mkdir -p src/PkLots.LprEngine
mkdir -p docker
mkdir -p docs

# Subpastas do projeto .NET
mkdir -p src/PkLots.Core/{Entities,Enums,Interfaces}
mkdir -p src/PkLots.Infrastructure/{Data,Repositories,Services}
mkdir -p src/PkLots.Application/{UseCases,DTOs,Mappings}
mkdir -p src/PkLots.Api/{Controllers,Middleware,Configuration}

# Subpastas do serviço Python
mkdir -p src/PkLots.LprEngine/{app,models,utils,services}

# Criar diretório de testes
mkdir -p tests/{PkLots.Api.Tests,PkLots.Infrastructure.Tests}

# Git
git init
echo "
# PkLots - Parking Lot Management System
Builds/
obj/
bin/
.vs/
.vscode/
__pycache__/
*.pyc
.env
.env.local
docker/data/
" > .gitignore

echo "✅ Estrutura de pastas criada!"
echo ""
echo "📁 Estrutura do projeto:"
tree -L 3 -I 'bin|obj'

echo ""
echo "⏭️  Próximo passo: criar os projetos .NET"
