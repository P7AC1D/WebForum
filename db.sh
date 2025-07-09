#!/bin/bash

# Database management script for Web Forum
# Usage: ./db.sh [command]

set -e

update_database() {
    echo "Applying EF Core migrations..."
    cd src/WebForum.Api
    if dotnet ef database update; then
        echo "Migrations applied successfully!"
    else
        echo "Failed to apply migrations. Make sure the .NET SDK is installed and the project builds successfully."
        exit 1
    fi
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
        echo "Usage: $0 {start|stop|restart|reset|migrate|logs|connect|backup|status}"
        echo ""
        echo "Commands:"
        echo "  start   - Start the PostgreSQL database and apply migrations"
        echo "  stop    - Stop all services"
        echo "  restart - Restart the database"
        echo "  reset   - Reset database and apply migrations (WARNING: deletes all data)"
        echo "  migrate - Apply EF Core migrations to the database"
        echo "  logs    - Show database logs"
        echo "  connect - Connect to database via psql"
        echo "  backup  - Create a database backup"
        echo "  status  - Show service status"
        exit 1
        ;;
esac
