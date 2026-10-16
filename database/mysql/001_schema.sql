-- MinhaBelém - MySQL 8.0+, executar dentro da base já criada pela Locaweb.
-- Não utiliza CREATE DATABASE ou CREATE USER (permissões restritas em hospedagem compartilhada).
CREATE TABLE IF NOT EXISTS editorial_users (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,
 email VARCHAR(254) NOT NULL UNIQUE,
 password_hash VARCHAR(500) NOT NULL,
 display_name VARCHAR(180) NOT NULL,
 role ENUM('superadmin','editor','moderator','advertiser') NOT NULL,
 enabled BOOLEAN NOT NULL DEFAULT TRUE,
 created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS ingestion_sources (
 id BIGINT AUTO_INCREMENT PRIMARY KEY, source_name VARCHAR(200) NOT NULL,
 feed_url VARCHAR(1500) NOT NULL, feed_url_hash BINARY(32) NOT NULL UNIQUE,
 protocol ENUM('rss','atom','gtfs','gtfs-rt','api') NOT NULL DEFAULT 'rss',
 enabled BOOLEAN NOT NULL DEFAULT FALSE, approved BOOLEAN NOT NULL DEFAULT FALSE,
 last_checked_at DATETIME(6), last_error TEXT, created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS ingestion_candidates (
 id BIGINT AUTO_INCREMENT PRIMARY KEY, source_id BIGINT NOT NULL,
 title VARCHAR(300) NOT NULL, summary VARCHAR(1500) NOT NULL DEFAULT '',
 source_url VARCHAR(2000) NOT NULL, source_url_hash BINARY(32) NOT NULL UNIQUE,
 published_at DATETIME(6) NOT NULL, status ENUM('pending','rejected','approved') NOT NULL DEFAULT 'pending',
 created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 FOREIGN KEY (source_id) REFERENCES ingestion_sources(id),
 INDEX idx_candidates_status (status,created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS content_items (
 id BIGINT AUTO_INCREMENT PRIMARY KEY, category VARCHAR(60) NOT NULL,
 title VARCHAR(300) NOT NULL, summary VARCHAR(2500) NOT NULL DEFAULT '',
 neighborhood VARCHAR(120), source_name VARCHAR(240) NOT NULL,
 source_url VARCHAR(2000) NOT NULL, source_url_hash BINARY(32) NOT NULL UNIQUE,
 author_name VARCHAR(240), image_url VARCHAR(2000), image_credit VARCHAR(300),
 image_license VARCHAR(120), address VARCHAR(500), event_at DATETIME(6),
 end_at DATETIME(6), ticket_price DECIMAL(12,2),
 published_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 last_verified_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 verification_status ENUM('pending','verified','outdated') NOT NULL DEFAULT 'pending',
 status ENUM('draft','review','published','archived') NOT NULL DEFAULT 'draft',
 reviewed_by BIGINT NULL, created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 FOREIGN KEY (reviewed_by) REFERENCES editorial_users(id),
 INDEX idx_content_public (status,category,published_at),
 CHECK(ticket_price IS NULL OR ticket_price >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS community_submissions (
 id BIGINT AUTO_INCREMENT PRIMARY KEY, display_name VARCHAR(100) NOT NULL,
 message VARCHAR(2500) NOT NULL,status ENUM('pending','approved','rejected') NOT NULL DEFAULT 'pending',
 created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 reviewed_by BIGINT NULL,FOREIGN KEY(reviewed_by) REFERENCES editorial_users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS transport_lines (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,agency VARCHAR(160) NOT NULL,
 line_code VARCHAR(50) NOT NULL,description TEXT,source_url VARCHAR(2000),
 last_verified_at DATETIME(6),UNIQUE KEY unique_line (agency,line_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS transport_stops (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,agency VARCHAR(160) NOT NULL,stop_code VARCHAR(150) NOT NULL,
 name VARCHAR(250) NOT NULL,latitude DECIMAL(10,7),longitude DECIMAL(10,7),
 UNIQUE KEY unique_stop (agency,stop_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS transport_alerts (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,title VARCHAR(300) NOT NULL,description TEXT NOT NULL,
 source_url VARCHAR(2000) NOT NULL,valid_from DATETIME(6) NOT NULL,valid_to DATETIME(6),
 verified BOOLEAN NOT NULL DEFAULT FALSE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS advertiser_campaigns (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,advertiser_name VARCHAR(250) NOT NULL,
 destination_url VARCHAR(2000) NOT NULL,image_url VARCHAR(2000),
 label VARCHAR(100) NOT NULL DEFAULT 'Publicidade',active BOOLEAN NOT NULL DEFAULT FALSE,
 valid_from DATETIME(6),valid_to DATETIME(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS ai_review_jobs (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,provider VARCHAR(60) NOT NULL,
 candidate_id BIGINT, status VARCHAR(30) NOT NULL DEFAULT 'pending',
 output_text LONGTEXT,created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 FOREIGN KEY(candidate_id) REFERENCES ingestion_candidates(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS audit_logs (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,actor_id BIGINT NULL,action VARCHAR(140) NOT NULL,
 entity_type VARCHAR(140) NOT NULL,entity_id BIGINT,details JSON,
 created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 FOREIGN KEY(actor_id) REFERENCES editorial_users(id),
 INDEX idx_audit_date (created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
