#!/bin/bash

# Database management script for Web Forum
# Usage: ./db.sh [command]

set -e

create_migration() {
    local migration_name=${1:-"InitialCreate"}
    echo "Creating EF Core migration: $migration_name..."
    cd src/WebForum.Api
    if dotnet ef migrations add "$migration_name"; then
        echo "Migration '$migration_name' created successfully!"
    else
        echo "Failed to create migration. Make sure the .NET SDK is installed and the project builds successfully."
        exit 1
    fi
    cd ../..
}

update_database() {
    echo "Applying EF Core migrations..."
    cd src/WebForum.Api
    
    # Check if Migrations folder exists and has migration files
    if [ ! -d "Migrations" ] || [ -z "$(find Migrations -name "*.cs" 2>/dev/null)" ]; then
        echo "No migrations found. Creating initial migration..."
        if dotnet ef migrations add "InitialCreate"; then
            echo "Initial migration created successfully!"
        else
            echo "Failed to create initial migration."
            exit 1
        fi
    fi
    
    if dotnet ef database update; then
        echo "Migrations applied successfully!"
    else
        echo "Failed to apply migrations. Make sure the .NET SDK is installed and the project builds successfully."
        exit 1
    fi
    cd ../..
}

remove_migration() {
    echo "Removing last EF Core migration..."
    cd src/WebForum.Api
    if dotnet ef migrations remove; then
        echo "Last migration removed successfully!"
    else
        echo "Failed to remove migration."
        exit 1
    fi
    cd ../..
}

list_migrations() {
    echo "Listing EF Core migrations..."
    cd src/WebForum.Api
    dotnet ef migrations list
    cd ../..
}

case "$1" in
    start)
        echo "Starting PostgreSQL database..."
        docker-compose up -d postgres
        echo "Database started. Waiting for it to be ready..."
        
        # Wait for database to be ready
        retries=30
        until docker-compose exec postgres pg_isready -U postgres -d webforum > /dev/null 2>&1; do
            if [ $retries -eq 0 ]; then
                echo "Database may not be ready yet. Check with 'docker-compose logs postgres'"
                exit 1
            fi
            sleep 1
            ((retries--))
        done
        
        echo "Database is ready!"
        update_database
        ;;
    stop)
        echo "Stopping database..."
        docker-compose down
        ;;
    restart)
        echo "Restarting database..."
        docker-compose restart postgres
        ;;
    reset)
        echo "WARNING: This will delete all data!"
        read -p "Are you sure? (y/N): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            docker-compose down -v
            docker-compose up -d postgres
            echo "Database reset complete! Waiting for database to be ready..."
            
            # Wait for database to be ready before applying migrations
            retries=30
            until docker-compose exec postgres pg_isready -U postgres -d webforum > /dev/null 2>&1; do
                if [ $retries -eq 0 ]; then
                    echo "Database may not be ready yet. Run './db.sh migrate' manually."
                    exit 1
                fi
                sleep 1
                ((retries--))
            done
            
            echo "Database is ready! Applying migrations..."
            update_database
        else
            echo "Reset cancelled."
        fi
        ;;
    migrate)
        update_database
        ;;
    create-migration)
        create_migration "$2"
        ;;
    remove-migration)
        remove_migration
        ;;
    list-migrations)
        list_migrations
        ;;
    logs)
        docker-compose logs -f postgres
        ;;
    connect)
        docker-compose exec postgres psql -U postgres -d webforum
        ;;
    backup)
        BACKUP_FILE="backup_$(date +%Y%m%d_%H%M%S).sql"
        echo "Creating backup: $BACKUP_FILE"
        docker-compose exec postgres pg_dump -U postgres webforum > "$BACKUP_FILE"
        echo "Backup created: $BACKUP_FILE"
        ;;
    status)
        docker-compose ps
        ;;
    *)
        echo "Web Forum Database Management"
        echo "Usage: $0 [command]"
        echo ""
        echo "Commands:"
        echo "  start                    - Start the PostgreSQL database and apply migrations"
        echo "  stop                     - Stop all services"
        echo "  restart                  - Restart the database"
        echo "  reset                    - Reset database and apply migrations (WARNING: deletes all data)"
        echo "  migrate                  - Apply EF Core migrations to the database"
        echo "  create-migration [name]  - Create a new EF Core migration (default: InitialCreate)"
        echo "  remove-migration         - Remove the last migration"
        echo "  list-migrations          - List all migrations"
        echo "  logs                     - Show database logs"
        echo "  connect                  - Connect to database via psql"
        echo "  backup                   - Create a database backup"
        echo "  status                   - Show service status"
        exit 1
        ;;
esac
