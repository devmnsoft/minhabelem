-- MINHABELEM.SQL - fonte unica do esquema MinhaBelem
-- MySQL 8.0+. Selecione a base do projeto no SGBD e execute TODO este arquivo.
-- O banco deve existir: nao exige CREATE DATABASE, CREATE USER ou privilegios administrativos.
-- Exemplo no cliente mysql: mysql -h HOST -u USUARIO -p NOME_DA_BASE < MINHABELEM.SQL
-- Preserva tabelas e dados existentes. CREATE TABLE IF NOT EXISTS nao atualiza colunas antigas.
-- Inclui tabelas do portal atual e tabelas mb_ da infraestrutura em integracao.
-- Nao copia automaticamente dados entre essas duas estruturas e nao cria usuario/senha de acesso.
-- DDL no MySQL tem commit implicito; uma falha pode deixar parte das tabelas criada.
-- Corrija a causa e reexecute o arquivo. Seeds nao sobrescrevem papeis/permissoes existentes.
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS schema_migrations (
    version VARCHAR(150) NOT NULL PRIMARY KEY,
    checksum CHAR(64) NOT NULL,
    applied_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    duration_ms INT NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- PARTE 1: tabelas usadas pelo portal atual.
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


-- PARTE 2: infraestrutura da Sprint 02.

-- 001_identidade.sql
-- Identidade ASP.NET Core (tabelas próprias, mantidas por Dapper) + permissões por funcionalidade.

CREATE TABLE IF NOT EXISTS mb_users (
    id                       BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_name                VARCHAR(256) NULL,
    normalized_user_name     VARCHAR(256) NULL,
    email                    VARCHAR(256) NULL,
    normalized_email         VARCHAR(256) NULL,
    email_confirmed          TINYINT(1)     NOT NULL DEFAULT 0,
    password_hash            VARCHAR(256) NULL,
    security_stamp           VARCHAR(32)  NULL,
    concurrency_stamp        VARCHAR(32)  NULL,
    lockout_enabled          TINYINT(1)     NOT NULL DEFAULT 1,
    lockout_end              DATETIME(6) NULL,
    access_failed_count      INT            NOT NULL DEFAULT 0,
    display_name             VARCHAR(180) NOT NULL DEFAULT '',
    enabled                  TINYINT(1)     NOT NULL DEFAULT 1,
    mfa_enabled              TINYINT(1)     NOT NULL DEFAULT 0,
    phone_number             VARCHAR(32)  NULL,
    phone_number_confirmed   TINYINT(1)     NOT NULL DEFAULT 0,
    two_factor_enabled       TINYINT(1)     NOT NULL DEFAULT 0,
    lockout_date             DATETIME(6) NULL,
    password_changed_at      DATETIME(6) NULL,
    last_login_at            DATETIME(6) NULL,
    created_at               DATETIME(6)    NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_users_normalized_name (normalized_user_name),
    UNIQUE KEY uq_users_normalized_email (normalized_email),
    KEY idx_users_display (display_name)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_roles (
    id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name               VARCHAR(256) NOT NULL,
    normalized_name    VARCHAR(256) NOT NULL,
    concurrency_stamp  VARCHAR(32)  NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_roles_normalized (normalized_name)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_user_roles (
    user_id   BIGINT UNSIGNED NOT NULL,
    role_id   BIGINT UNSIGNED NOT NULL,
    PRIMARY KEY (user_id, role_id),
    KEY idx_user_roles_role (role_id),
    CONSTRAINT fk_user_roles_user FOREIGN KEY (user_id) REFERENCES mb_users (id) ON DELETE CASCADE,
    CONSTRAINT fk_user_roles_role FOREIGN KEY (role_id) REFERENCES mb_roles (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_user_claims (
    id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_id      BIGINT UNSIGNED NOT NULL,
    claim_type   VARCHAR(256) NOT NULL,
    claim_value  VARCHAR(1024) NULL,
    PRIMARY KEY (id),
    KEY idx_user_claims_user (user_id, claim_type),
    CONSTRAINT fk_user_claims_user FOREIGN KEY (user_id) REFERENCES mb_users (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_role_claims (
    id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    role_id      BIGINT UNSIGNED NOT NULL,
    claim_type   VARCHAR(256) NOT NULL,
    claim_value  VARCHAR(1024) NULL,
    PRIMARY KEY (id),
    KEY idx_role_claims_role (role_id, claim_type),
    CONSTRAINT fk_role_claims_role FOREIGN KEY (role_id) REFERENCES mb_roles (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_user_logins (
    login_provider          VARCHAR(128) NOT NULL,
    provider_key            VARCHAR(256) NOT NULL,
    provider_display_name   VARCHAR(256) NULL,
    user_id                 BIGINT UNSIGNED NOT NULL,
    PRIMARY KEY (login_provider, provider_key),
    KEY idx_user_logins_user (user_id),
    CONSTRAINT fk_user_logins_user FOREIGN KEY (user_id) REFERENCES mb_users (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_user_tokens (
    user_id          BIGINT UNSIGNED NOT NULL,
    login_provider   VARCHAR(128) NOT NULL,
    name             VARCHAR(128) NOT NULL,
    value_cipher     VARCHAR(2048) NOT NULL,
    PRIMARY KEY (user_id, login_provider, name),
    CONSTRAINT fk_user_tokens_user FOREIGN KEY (user_id) REFERENCES mb_users (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_permissions (
    code        VARCHAR(64)  NOT NULL,
    description VARCHAR(255) NOT NULL,
    PRIMARY KEY (code)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_role_permissions (
    role_id         BIGINT UNSIGNED NOT NULL,
    permission_code VARCHAR(64)     NOT NULL,
    PRIMARY KEY (role_id, permission_code),
    KEY idx_role_permissions_perm (permission_code),
    CONSTRAINT fk_role_perm_role FOREIGN KEY (role_id) REFERENCES mb_roles (id) ON DELETE CASCADE,
    CONSTRAINT fk_role_perm_perm FOREIGN KEY (permission_code) REFERENCES mb_permissions (code) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_audit_logs (
    id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    actor_id     BIGINT UNSIGNED NULL,
    actor_name   VARCHAR(180) NULL,
    action       VARCHAR(140) NOT NULL,
    entity_type  VARCHAR(140) NOT NULL,
    entity_id    BIGINT UNSIGNED NULL,
    details      JSON NULL,
    ip_address   VARCHAR(45)  NULL,
    created_at   DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    KEY idx_audit_created (created_at),
    KEY idx_audit_entity (entity_type, entity_id),
    KEY idx_audit_actor (actor_id)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- 002_conteudo.sql
-- Conteúdo editorial, revisões, públicos e recursos de acessibilidade.

CREATE TABLE IF NOT EXISTS mb_content_items (
    id                     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    kind                   VARCHAR(24)  NOT NULL,
    category               VARCHAR(32)  NOT NULL,
    subject                VARCHAR(64)  NULL,
    title                  VARCHAR(300) NOT NULL,
    slug                   VARCHAR(180) NOT NULL,
    summary                VARCHAR(2500) NOT NULL DEFAULT '',
    body                   MEDIUMTEXT NULL,
    neighborhood           VARCHAR(120) NULL,
    municipality           VARCHAR(120) NULL,
    state                  CHAR(2)      NOT NULL DEFAULT 'PA',
    address                VARCHAR(500) NULL,
    latitude               DECIMAL(10, 7) NULL,
    longitude              DECIMAL(10, 7) NULL,
    source_id              BIGINT UNSIGNED NULL,
    source_name            VARCHAR(240) NOT NULL,
    source_url             VARCHAR(2000) NOT NULL,
    source_url_hash        BINARY(32)   NOT NULL,
    fingerprint            CHAR(64)     NOT NULL,
    author_name            VARCHAR(240) NULL,
    source_published_at    DATETIME(6) NULL,
    image_url              VARCHAR(2000) NULL,
    image_credit           VARCHAR(300) NULL,
    image_license          VARCHAR(120) NULL,
    event_start_at         DATETIME(6) NULL,
    event_end_at           DATETIME(6) NULL,
    ticket_price           DECIMAL(12, 2) NULL,
    ticket_price_text      VARCHAR(120) NULL,
    is_free                TINYINT(1)   NOT NULL DEFAULT 0,
    status                 VARCHAR(24)  NOT NULL DEFAULT 'Draft',
    verification_status    VARCHAR(24)  NOT NULL DEFAULT 'Pending',
    flags                  INT          NOT NULL DEFAULT 0,
    duplicate_of_id        BIGINT UNSIGNED NULL,
    requires_human_approval TINYINT(1)  NOT NULL DEFAULT 1,
    published_at           DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    expires_at             DATETIME(6) NULL,
    last_verified_at       DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    reviewed_by_id         BIGINT UNSIGNED NULL,
    created_by_id          BIGINT UNSIGNED NULL,
    revision               INT          NOT NULL DEFAULT 1,
    created_at             DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at             DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_content_slug (slug),
    UNIQUE KEY uq_content_source_url (source_url_hash),
    KEY idx_content_public (status, published_at),
    KEY idx_content_category (category, status, published_at),
    KEY idx_content_event (status, event_start_at),
    KEY idx_content_neighborhood (neighborhood),
    KEY idx_content_municipality (municipality),
    KEY idx_content_status_updated (status, updated_at),
    KEY idx_content_fingerprint (fingerprint),
    CONSTRAINT fk_content_duplicate FOREIGN KEY (duplicate_of_id) REFERENCES mb_content_items (id) ON DELETE SET NULL,
    CONSTRAINT chk_content_price CHECK (ticket_price IS NULL OR ticket_price >= 0)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_content_audiences (
    content_id   BIGINT UNSIGNED NOT NULL,
    audience     VARCHAR(24)  NOT NULL,
    PRIMARY KEY (content_id, audience),
    CONSTRAINT fk_content_aud_content FOREIGN KEY (content_id) REFERENCES mb_content_items (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_content_accessibility (
    content_id   BIGINT UNSIGNED NOT NULL,
    feature      VARCHAR(32)  NOT NULL,
    PRIMARY KEY (content_id, feature),
    CONSTRAINT fk_content_acc_content FOREIGN KEY (content_id) REFERENCES mb_content_items (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_content_revisions (
    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    content_id      BIGINT UNSIGNED NOT NULL,
    revision        INT          NOT NULL,
    change_summary  VARCHAR(500) NULL,
    snapshot        JSON         NOT NULL,
    changed_by_id   BIGINT UNSIGNED NULL,
    changed_by_name VARCHAR(180) NULL,
    changed_at      DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_content_revision (content_id, revision),
    CONSTRAINT fk_revisions_content FOREIGN KEY (content_id) REFERENCES mb_content_items (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- 003_coleta.sql
-- Fontes de coleta, execuções (histórico) e candidatos.

CREATE TABLE IF NOT EXISTS mb_ingestion_sources (
    id                     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name                   VARCHAR(200) NOT NULL,
    feed_url               VARCHAR(2000) NOT NULL,
    feed_url_hash          BINARY(32)   NOT NULL,
    protocol               VARCHAR(16)  NOT NULL DEFAULT 'Rss',
    topics                 VARCHAR(255) NULL,
    municipality           VARCHAR(120) NULL,
    enabled                TINYINT(1)   NOT NULL DEFAULT 0,
    approved               TINYINT(1)   NOT NULL DEFAULT 0,
    respect_robots         TINYINT(1)   NOT NULL DEFAULT 1,
    license_note           VARCHAR(500) NULL,
    last_checked_at        DATETIME(6) NULL,
    last_success_at        DATETIME(6) NULL,
    last_error             TEXT NULL,
    consecutive_failures   INT          NOT NULL DEFAULT 0,
    etag                   VARCHAR(200) NULL,
    last_modified          DATETIME(6) NULL,
    created_at             DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_source_url (feed_url_hash),
    KEY idx_source_operational (enabled, approved)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_ingestion_runs (
    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    started_at      DATETIME(6) NOT NULL,
    finished_at     DATETIME(6) NOT NULL,
    status          VARCHAR(16)  NOT NULL,
    sources_checked INT          NOT NULL DEFAULT 0,
    sources_failed  INT          NOT NULL DEFAULT 0,
    items_seen      INT          NOT NULL DEFAULT 0,
    items_inserted  INT          NOT NULL DEFAULT 0,
    items_duplicate INT          NOT NULL DEFAULT 0,
    error           TEXT NULL,
    PRIMARY KEY (id),
    KEY idx_runs_started (started_at)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_ingestion_candidates (
    id                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    source_id         BIGINT UNSIGNED NOT NULL,
    source_name       VARCHAR(200) NOT NULL,
    run_id            BIGINT UNSIGNED NULL,
    external_id       VARCHAR(300) NULL,
    title             VARCHAR(300) NOT NULL,
    summary           VARCHAR(1500) NOT NULL DEFAULT '',
    source_url        VARCHAR(2000) NOT NULL,
    source_url_hash   BINARY(32)   NOT NULL,
    fingerprint       CHAR(64)     NOT NULL,
    author_name       VARCHAR(240) NULL,
    published_at      DATETIME(6)  NOT NULL,
    image_url         VARCHAR(2000) NULL,
    image_credit      VARCHAR(300) NULL,
    status            VARCHAR(16)  NOT NULL DEFAULT 'Pending',
    duplicate_of_id   BIGINT UNSIGNED NULL,
    content_id        BIGINT UNSIGNED NULL,
    review_notes      VARCHAR(1000) NULL,
    created_at        DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at        DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_candidate_url (source_url_hash),
    KEY idx_candidate_status (status, created_at),
    KEY idx_candidate_source (source_id),
    KEY idx_candidate_fingerprint (fingerprint),
    CONSTRAINT fk_candidate_source FOREIGN KEY (source_id) REFERENCES mb_ingestion_sources (id) ON DELETE CASCADE,
    CONSTRAINT fk_candidate_run FOREIGN KEY (run_id) REFERENCES mb_ingestion_runs (id) ON DELETE SET NULL,
    CONSTRAINT fk_candidate_duplicate FOREIGN KEY (duplicate_of_id) REFERENCES mb_ingestion_candidates (id) ON DELETE SET NULL
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- 004_ia.sql
-- Registro obrigatório de cada execução de IA: provedor, modelo, versão, custo, latência e resultado.

CREATE TABLE IF NOT EXISTS mb_ai_runs (
    id                   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    provider             VARCHAR(24)  NOT NULL,
    model                VARCHAR(120) NOT NULL,
    model_version        VARCHAR(120) NOT NULL DEFAULT '',
    task                 VARCHAR(32)  NOT NULL,
    target_type          VARCHAR(40)  NOT NULL,
    target_id            BIGINT UNSIGNED NULL,
    prompt_chars         INT          NOT NULL DEFAULT 0,
    prompt_tokens        INT          NULL,
    completion_tokens    INT          NULL,
    cost_usd             DECIMAL(12, 6) NOT NULL DEFAULT 0,
    latency_ms           INT          NOT NULL DEFAULT 0,
    success              TINYINT(1)   NOT NULL DEFAULT 0,
    error                TEXT NULL,
    response_json        JSON NULL,
    triggered_by_user_id BIGINT UNSIGNED NULL,
    created_at           DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    KEY idx_ai_created (created_at),
    KEY idx_ai_task (task, created_at),
    KEY idx_ai_provider (provider, created_at)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- 005_guia.sql
-- Lugares do guia (Google Places). O place_id é armazenado de forma permanente; o restante
-- tem prazo de expurgo para atender à política de armazenamento de conteúdo do Google Places.

CREATE TABLE IF NOT EXISTS mb_places (
    id                     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    google_place_id        VARCHAR(255) NOT NULL,
    slug                   VARCHAR(180) NOT NULL,
    name                   VARCHAR(300) NOT NULL,
    formatted_address      VARCHAR(500) NULL,
    neighborhood           VARCHAR(120) NULL,
    municipality           VARCHAR(120) NULL,
    state                  CHAR(2)      NOT NULL DEFAULT 'PA',
    latitude               DECIMAL(10, 7) NULL,
    longitude              DECIMAL(10, 7) NULL,
    google_maps_uri        VARCHAR(2000) NULL,
    website_uri            VARCHAR(2000) NULL,
    phone                  VARCHAR(64)  NULL,
    primary_type           VARCHAR(80)  NULL,
    types                  JSON NULL,
    rating                 DECIMAL(3, 2) NULL,
    rating_count           INT NULL,
    business_status        VARCHAR(32)  NULL,
    opening_hours          JSON NULL,
    price_level            TINYINT NULL,
    price_text             VARCHAR(120) NULL,
    wheelchair_accessible TINYINT(1) NULL,
    photo_name             VARCHAR(255) NULL,
    photo_attribution      VARCHAR(500) NULL,
    is_published           TINYINT(1)   NOT NULL DEFAULT 1,
    editorial_note         VARCHAR(1000) NULL,
    editorial_note_source  VARCHAR(2000) NULL,
    cached_at              DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    purge_after            DATETIME(6)  NOT NULL,
    created_at             DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at             DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_place_google (google_place_id),
    UNIQUE KEY uq_place_slug (slug),
    KEY idx_place_geo (latitude, longitude),
    KEY idx_place_neighborhood (neighborhood),
    KEY idx_place_municipality (municipality),
    KEY idx_place_type (primary_type),
    KEY idx_place_purge (purge_after),
    FULLTEXT KEY ft_place_name (name, formatted_address)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_place_audiences (
    place_id   BIGINT UNSIGNED NOT NULL,
    audience   VARCHAR(24)  NOT NULL,
    PRIMARY KEY (place_id, audience),
    CONSTRAINT fk_place_aud_place FOREIGN KEY (place_id) REFERENCES mb_places (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_place_accessibility (
    place_id   BIGINT UNSIGNED NOT NULL,
    feature    VARCHAR(32)  NOT NULL,
    PRIMARY KEY (place_id, feature),
    CONSTRAINT fk_place_acc_place FOREIGN KEY (place_id) REFERENCES mb_places (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- 006_plataforma.sql
CREATE TABLE IF NOT EXISTS mb_community_submissions (
    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    display_name    VARCHAR(100) NOT NULL,
    contact         VARCHAR(160) NULL,
    message         VARCHAR(2500) NOT NULL,
    status          VARCHAR(16)  NOT NULL DEFAULT 'Pending',
    review_notes    VARCHAR(1000) NULL,
    reviewed_by_id  BIGINT UNSIGNED NULL,
    created_at      DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at      DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    KEY idx_submissions_status (status, created_at)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_advertiser_campaigns (
    id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    advertiser_name    VARCHAR(250) NOT NULL,
    destination_url    VARCHAR(2000) NOT NULL,
    image_url          VARCHAR(2000) NULL,
    label              VARCHAR(100) NOT NULL DEFAULT 'Publicidade',
    headline           VARCHAR(200) NULL,
    active             TINYINT(1)   NOT NULL DEFAULT 0,
    valid_from         DATETIME(6) NULL,
    valid_to           DATETIME(6) NULL,
    advertiser_user_id VARCHAR(256) NULL,
    created_at         DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    KEY idx_campaigns_active (active, valid_from, valid_to)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_localities (
    id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name        VARCHAR(120) NOT NULL,
    slug        VARCHAR(140) NOT NULL,
    kind        VARCHAR(16)  NOT NULL,
    state       CHAR(2)      NOT NULL DEFAULT 'PA',
    active      TINYINT(1)   NOT NULL DEFAULT 1,
    PRIMARY KEY (id),
    UNIQUE KEY uq_locality (slug, kind)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_settings (
    setting_key   VARCHAR(80)  NOT NULL,
    setting_value TEXT NULL,
    updated_at    DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (setting_key)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- PARTE 3: papeis e permissoes, sem IDs fixos.
INSERT IGNORE INTO mb_permissions (code, description) VALUES
 ('dashboard.read',      'Ver painel com indicadores reais'),
 ('content.read',        'Ver conteúdo em qualquer estado'),
 ('content.write',       'Criar e editar conteúdo'),
 ('content.publish',     'Publicar, despublicar e arquivar'),
 ('content.delete',      'Excluir conteúdo definitivamente'),
 ('candidate.read',      'Ver candidatos coletados'),
 ('candidate.review',    'Aprovar ou rejeitar candidatos'),
 ('source.manage',       'Cadastrar e ativar fontes de coleta'),
 ('place.manage',        'Curar lugares do guia'),
 ('submission.moderate', 'Moderar colaborações da comunidade'),
 ('advertiser.manage',   'Gerenciar campanhas de anunciantes'),
 ('ai.run',              'Executar análise de inteligência artificial'),
 ('audit.read',          'Consultar trilha de auditoria'),
 ('user.manage',         'Gerenciar usuários, papéis e permissões'),
 ('settings.manage',     'Alterar configurações da plataforma');

INSERT IGNORE INTO mb_roles (name, normalized_name, concurrency_stamp) VALUES
 ('SuperAdmin', 'SUPERADMIN', 'seed0000000000000000000000000001'),
 ('Editor', 'EDITOR', 'seed0000000000000000000000000002'),
 ('Moderator', 'MODERATOR', 'seed0000000000000000000000000003'),
 ('Advertiser', 'ADVERTISER', 'seed0000000000000000000000000004');
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'dashboard.read' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.read' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.write' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.publish' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.delete' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'candidate.read' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'candidate.review' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'source.manage' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'place.manage' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'submission.moderate' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'advertiser.manage' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'ai.run' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'audit.read' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'user.manage' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'settings.manage' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'dashboard.read' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.read' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.write' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.publish' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'candidate.read' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'candidate.review' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'source.manage' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'place.manage' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'submission.moderate' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'ai.run' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'dashboard.read' FROM mb_roles WHERE normalized_name = 'MODERATOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.read' FROM mb_roles WHERE normalized_name = 'MODERATOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'candidate.read' FROM mb_roles WHERE normalized_name = 'MODERATOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'candidate.review' FROM mb_roles WHERE normalized_name = 'MODERATOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'place.manage' FROM mb_roles WHERE normalized_name = 'MODERATOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'submission.moderate' FROM mb_roles WHERE normalized_name = 'MODERATOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'ai.run' FROM mb_roles WHERE normalized_name = 'MODERATOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'advertiser.manage' FROM mb_roles WHERE normalized_name = 'ADVERTISER';

-- Fim do script. Fontes e usuarios devem ser cadastrados pela aplicacao.

-- @migration 003_operacao
-- Sprint 03: incremento aditivo. Preserva dados e tabelas anteriores.
CREATE TABLE IF NOT EXISTS mb_user_sessions (
    id CHAR(32) NOT NULL PRIMARY KEY,
    user_id BIGINT UNSIGNED NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    expires_at DATETIME(6) NOT NULL,
    revoked_at DATETIME(6) NULL,
    KEY idx_session_user (user_id, expires_at),
    CONSTRAINT fk_session_user FOREIGN KEY (user_id) REFERENCES mb_users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- @migration 004_descoberta
-- Sprint 03B: configuracao, limites e evidencias de descoberta.
CREATE TABLE IF NOT EXISTS mb_source_profiles (
    source_id BIGINT UNSIGNED NOT NULL PRIMARY KEY,
    owner_name VARCHAR(180) NOT NULL DEFAULT '',
    organization VARCHAR(200) NOT NULL DEFAULT '',
    region VARCHAR(120) NOT NULL DEFAULT 'Pará',
    keywords VARCHAR(500) NOT NULL DEFAULT '',
    frequency_minutes INT NOT NULL DEFAULT 10,
    daily_request_limit INT NOT NULL DEFAULT 24,
    max_items INT NOT NULL DEFAULT 20,
    credential_reference VARCHAR(80) NULL,
    checkpoint VARCHAR(1000) NULL,
    status VARCHAR(40) NOT NULL DEFAULT 'AwaitingConfiguration',
    next_due_at DATETIME(6) NULL,
    records_found BIGINT NOT NULL DEFAULT 0,
    CONSTRAINT fk_profile_source FOREIGN KEY (source_id) REFERENCES mb_ingestion_sources(id) ON DELETE CASCADE,
    CONSTRAINT chk_source_frequency CHECK (frequency_minutes BETWEEN 10 AND 1440),
    CONSTRAINT chk_source_requests CHECK (daily_request_limit BETWEEN 1 AND 1000)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS mb_source_requests (
    source_id BIGINT UNSIGNED NOT NULL,
    day DATE NOT NULL,
    requests INT NOT NULL DEFAULT 0,
    PRIMARY KEY(source_id,day),
    CONSTRAINT fk_request_source FOREIGN KEY (source_id) REFERENCES mb_ingestion_sources(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS mb_discovery_versions (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    candidate_id BIGINT UNSIGNED NOT NULL,
    fingerprint CHAR(64) NOT NULL,
    snapshot JSON NOT NULL,
    discovered_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UNIQUE KEY uq_discovery_version(candidate_id,fingerprint),
    CONSTRAINT fk_discovery_candidate FOREIGN KEY(candidate_id) REFERENCES mb_ingestion_candidates(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS mb_event_evidence (
    content_id BIGINT UNSIGNED NOT NULL PRIMARY KEY,
    evidence JSON NOT NULL,
    CONSTRAINT fk_event_evidence FOREIGN KEY(content_id) REFERENCES mb_content_items(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS mb_api_usage (
    service VARCHAR(64) NOT NULL,
    day DATE NOT NULL,
    requests INT NOT NULL DEFAULT 0,
    PRIMARY KEY(service,day)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- @migration 005_localidades
-- Municipios: API IBGE UF 15, consulta 16/10/2026. Bairros: catalogo CODEM de referencia.
INSERT IGNORE INTO mb_localities (name,slug,kind) VALUES
('Abaetetuba','abaetetuba','municipality'),
('Abel Figueiredo','abel-figueiredo','municipality'),
('Acará','acara','municipality'),
('Afuá','afua','municipality'),
('Água Azul do Norte','agua-azul-do-norte','municipality'),
('Alenquer','alenquer','municipality'),
('Almeirim','almeirim','municipality'),
('Altamira','altamira','municipality'),
('Anajás','anajas','municipality'),
('Ananindeua','ananindeua','municipality'),
('Anapu','anapu','municipality'),
('Augusto Corrêa','augusto-correa','municipality'),
('Aurora do Pará','aurora-do-para','municipality'),
('Aveiro','aveiro','municipality'),
('Bagre','bagre','municipality'),
('Baião','baiao','municipality'),
('Bannach','bannach','municipality'),
('Barcarena','barcarena','municipality'),
('Belém','belem','municipality'),
('Belterra','belterra','municipality'),
('Benevides','benevides','municipality'),
('Bom Jesus do Tocantins','bom-jesus-do-tocantins','municipality'),
('Bonito','bonito','municipality'),
('Bragança','braganca','municipality'),
('Brasil Novo','brasil-novo','municipality'),
('Brejo Grande do Araguaia','brejo-grande-do-araguaia','municipality'),
('Breu Branco','breu-branco','municipality'),
('Breves','breves','municipality'),
('Bujaru','bujaru','municipality'),
('Cachoeira do Arari','cachoeira-do-arari','municipality'),
('Cachoeira do Piriá','cachoeira-do-piria','municipality'),
('Cametá','cameta','municipality'),
('Canaã dos Carajás','canaa-dos-carajas','municipality'),
('Capanema','capanema','municipality'),
('Capitão Poço','capitao-poco','municipality'),
('Castanhal','castanhal','municipality'),
('Chaves','chaves','municipality'),
('Colares','colares','municipality'),
('Conceição do Araguaia','conceicao-do-araguaia','municipality'),
('Concórdia do Pará','concordia-do-para','municipality'),
('Cumaru do Norte','cumaru-do-norte','municipality'),
('Curionópolis','curionopolis','municipality'),
('Curralinho','curralinho','municipality'),
('Curuá','curua','municipality'),
('Curuçá','curuca','municipality'),
('Dom Eliseu','dom-eliseu','municipality'),
('Eldorado do Carajás','eldorado-do-carajas','municipality'),
('Faro','faro','municipality'),
('Floresta do Araguaia','floresta-do-araguaia','municipality'),
('Garrafão do Norte','garrafao-do-norte','municipality'),
('Goianésia do Pará','goianesia-do-para','municipality'),
('Gurupá','gurupa','municipality'),
('Igarapé-Açu','igarape-acu','municipality'),
('Igarapé-Miri','igarape-miri','municipality'),
('Inhangapi','inhangapi','municipality'),
('Ipixuna do Pará','ipixuna-do-para','municipality'),
('Irituia','irituia','municipality'),
('Itaituba','itaituba','municipality'),
('Itupiranga','itupiranga','municipality'),
('Jacareacanga','jacareacanga','municipality'),
('Jacundá','jacunda','municipality'),
('Juruti','juruti','municipality'),
('Limoeiro do Ajuru','limoeiro-do-ajuru','municipality'),
('Mãe do Rio','mae-do-rio','municipality'),
('Magalhães Barata','magalhaes-barata','municipality'),
('Marabá','maraba','municipality'),
('Maracanã','maracana','municipality'),
('Marapanim','marapanim','municipality'),
('Marituba','marituba','municipality'),
('Medicilândia','medicilandia','municipality'),
('Melgaço','melgaco','municipality'),
('Mocajuba','mocajuba','municipality'),
('Moju','moju','municipality'),
('Mojuí dos Campos','mojui-dos-campos','municipality'),
('Monte Alegre','monte-alegre','municipality'),
('Muaná','muana','municipality'),
('Nova Esperança do Piriá','nova-esperanca-do-piria','municipality'),
('Nova Ipixuna','nova-ipixuna','municipality'),
('Nova Timboteua','nova-timboteua','municipality'),
('Novo Progresso','novo-progresso','municipality'),
('Novo Repartimento','novo-repartimento','municipality'),
('Óbidos','obidos','municipality'),
('Oeiras do Pará','oeiras-do-para','municipality'),
('Oriximiná','oriximina','municipality'),
('Ourém','ourem','municipality'),
('Ourilândia do Norte','ourilandia-do-norte','municipality'),
('Pacajá','pacaja','municipality'),
('Palestina do Pará','palestina-do-para','municipality'),
('Paragominas','paragominas','municipality'),
('Parauapebas','parauapebas','municipality'),
('Pau D''Arco','pau-d-arco','municipality'),
('Peixe-Boi','peixe-boi','municipality'),
('Piçarra','picarra','municipality'),
('Placas','placas','municipality'),
('Ponta de Pedras','ponta-de-pedras','municipality'),
('Portel','portel','municipality'),
('Porto de Moz','porto-de-moz','municipality'),
('Prainha','prainha','municipality'),
('Primavera','primavera','municipality'),
('Quatipuru','quatipuru','municipality'),
('Redenção','redencao','municipality'),
('Rio Maria','rio-maria','municipality'),
('Rondon do Pará','rondon-do-para','municipality'),
('Rurópolis','ruropolis','municipality'),
('Salinópolis','salinopolis','municipality'),
('Salvaterra','salvaterra','municipality'),
('Santa Bárbara do Pará','santa-barbara-do-para','municipality'),
('Santa Cruz do Arari','santa-cruz-do-arari','municipality'),
('Santa Izabel do Pará','santa-izabel-do-para','municipality'),
('Santa Luzia do Pará','santa-luzia-do-para','municipality'),
('Santa Maria das Barreiras','santa-maria-das-barreiras','municipality'),
('Santa Maria do Pará','santa-maria-do-para','municipality'),
('Santana do Araguaia','santana-do-araguaia','municipality'),
('Santarém','santarem','municipality'),
('Santarém Novo','santarem-novo','municipality'),
('Santo Antônio do Tauá','santo-antonio-do-taua','municipality'),
('São Caetano de Odivelas','sao-caetano-de-odivelas','municipality'),
('São Domingos do Araguaia','sao-domingos-do-araguaia','municipality'),
('São Domingos do Capim','sao-domingos-do-capim','municipality'),
('São Félix do Xingu','sao-felix-do-xingu','municipality'),
('São Francisco do Pará','sao-francisco-do-para','municipality'),
('São Geraldo do Araguaia','sao-geraldo-do-araguaia','municipality'),
('São João da Ponta','sao-joao-da-ponta','municipality'),
('São João de Pirabas','sao-joao-de-pirabas','municipality'),
('São João do Araguaia','sao-joao-do-araguaia','municipality'),
('São Miguel do Guamá','sao-miguel-do-guama','municipality'),
('São Sebastião da Boa Vista','sao-sebastiao-da-boa-vista','municipality'),
('Sapucaia','sapucaia','municipality'),
('Senador José Porfírio','senador-jose-porfirio','municipality'),
('Soure','soure','municipality'),
('Tailândia','tailandia','municipality'),
('Terra Alta','terra-alta','municipality'),
('Terra Santa','terra-santa','municipality'),
('Tomé-Açu','tome-acu','municipality'),
('Tracuateua','tracuateua','municipality'),
('Trairão','trairao','municipality'),
('Tucumã','tucuma','municipality'),
('Tucuruí','tucurui','municipality'),
('Ulianópolis','ulianopolis','municipality'),
('Uruará','uruara','municipality'),
('Vigia','vigia','municipality'),
('Viseu','viseu','municipality'),
('Vitória do Xingu','vitoria-do-xingu','municipality'),
('Xinguara','xinguara','municipality'),
('Cidade Velha','cidade-velha','neighborhood'),
('Campina','campina','neighborhood'),
('Reduto','reduto','neighborhood'),
('Umarizal','umarizal','neighborhood'),
('Telegrafo','telegrafo','neighborhood'),
('Sacramenta','sacramenta','neighborhood'),
('Pedreira','pedreira','neighborhood'),
('Marco','marco','neighborhood'),
('Souza','souza','neighborhood'),
('Marambaia','marambaia','neighborhood'),
('Canudos','canudos','neighborhood'),
('Fátima','fatima','neighborhood'),
('São Brás','sao-bras','neighborhood'),
('Nazaré','nazare','neighborhood'),
('Batista Campos','batista-campos','neighborhood'),
('Jurunas','jurunas','neighborhood'),
('Condor','condor','neighborhood'),
('Guamá','guama','neighborhood'),
('Terra Firme','terra-firme','neighborhood'),
('Cremação','cremacao','neighborhood'),
('Val-de-Cães','val-de-caes','neighborhood'),
('Miramar','miramar','neighborhood'),
('Pratinha','pratinha','neighborhood'),
('Tapanã','tapana','neighborhood'),
('Bengui','bengui','neighborhood'),
('Maracangalha','maracangalha','neighborhood'),
('Barreiro','barreiro','neighborhood'),
('Universitário','universitario','neighborhood'),
('Curió-Utinga','curio-utinga','neighborhood'),
('Aurá','aura','neighborhood'),
('Castanheira','castanheira','neighborhood'),
('Águas Lindas','aguas-lindas','neighborhood'),
('Guanabara','guanabara','neighborhood'),
('São Clemente','sao-clemente','neighborhood'),
('Parque Guajará','parque-guajara','neighborhood'),
('Tenoné','tenone','neighborhood'),
('Águas Negras','aguas-negras','neighborhood'),
('Maracacuera','maracacuera','neighborhood'),
('Parque Verde','parque-verde','neighborhood'),
('Cruzeiro','cruzeiro','neighborhood'),
('Ponta Grossa','ponta-grossa','neighborhood'),
('Mangueirão','mangueirao','neighborhood'),
('Cabanagem','cabanagem','neighborhood'),
('Campina de Icoaraci','campina-de-icoaraci','neighborhood'),
('Paracuri','paracuri','neighborhood'),
('Agulha','agulha','neighborhood'),
('Una','una','neighborhood'),
('Coqueiro','coqueiro','neighborhood'),
('Brasília','brasilia','neighborhood'),
('São João do Outeiro','sao-joao-do-outeiro','neighborhood'),
('Itaiteua','itaiteua','neighborhood'),
('Água Negra','agua-negra','neighborhood'),
('Maracajá','maracaja','neighborhood'),
('Vila','vila','neighborhood'),
('Praia Grande','praia-grande','neighborhood'),
('Farol','farol','neighborhood'),
('Mangueiras','mangueiras','neighborhood'),
('São Francisco','sao-francisco','neighborhood'),
('Caranamduba','caranamduba','neighborhood'),
('Marahu','marahu','neighborhood'),
('Paraíso','paraiso','neighborhood'),
('Baia do Sol','baia-do-sol','neighborhood'),
('Sucurijuquara','sucurijuquara','neighborhood'),
('Caruara','caruara','neighborhood'),
('Bonfim','bonfim','neighborhood'),
('Ariramba','ariramba','neighborhood'),
('Porto Arthur','porto-arthur','neighborhood'),
('Murubira','murubira','neighborhood'),
('Chapéu Virado','chapeu-virado','neighborhood'),
('Aeroporto','aeroporto','neighborhood');

-- @migration 006_midia
CREATE TABLE IF NOT EXISTS mb_media_permissions (
 source_id BIGINT UNSIGNED NOT NULL PRIMARY KEY,
 allow_analysis TINYINT(1) NOT NULL DEFAULT 0,
 license_note VARCHAR(1000) NOT NULL,
 CONSTRAINT fk_media_permission_source FOREIGN KEY(source_id) REFERENCES mb_ingestion_sources(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS mb_media_analysis (
 id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
 candidate_id BIGINT UNSIGNED NOT NULL,
 actor_id BIGINT UNSIGNED NOT NULL,
 engine VARCHAR(80) NOT NULL,
 extracted_text TEXT NOT NULL,
 credit VARCHAR(500) NOT NULL,
 license_note VARCHAR(1000) NOT NULL,
 created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 KEY idx_media_candidate(candidate_id,created_at),
 CONSTRAINT fk_media_candidate FOREIGN KEY(candidate_id) REFERENCES mb_ingestion_candidates(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- @migration 007_relacionamentos
-- Validacao aditiva: dados orfaos fazem a operacao parar; nenhum registro e apagado.

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_content_source'),'SELECT 1','ALTER TABLE mb_content_items ADD CONSTRAINT fk_mb_content_source FOREIGN KEY (source_id) REFERENCES mb_ingestion_sources(id) ON DELETE SET NULL');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_content_reviewer'),'SELECT 1','ALTER TABLE mb_content_items ADD CONSTRAINT fk_mb_content_reviewer FOREIGN KEY (reviewed_by_id) REFERENCES mb_users(id) ON DELETE SET NULL');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_content_author'),'SELECT 1','ALTER TABLE mb_content_items ADD CONSTRAINT fk_mb_content_author FOREIGN KEY (created_by_id) REFERENCES mb_users(id) ON DELETE SET NULL');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_revision_actor'),'SELECT 1','ALTER TABLE mb_content_revisions ADD CONSTRAINT fk_mb_revision_actor FOREIGN KEY (changed_by_id) REFERENCES mb_users(id) ON DELETE SET NULL');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_candidate_content'),'SELECT 1','ALTER TABLE mb_ingestion_candidates ADD CONSTRAINT fk_mb_candidate_content FOREIGN KEY (content_id) REFERENCES mb_content_items(id) ON DELETE SET NULL');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_audit_actor'),'SELECT 1','ALTER TABLE mb_audit_logs ADD CONSTRAINT fk_mb_audit_actor FOREIGN KEY (actor_id) REFERENCES mb_users(id) ON DELETE SET NULL');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_ai_actor'),'SELECT 1','ALTER TABLE mb_ai_runs ADD CONSTRAINT fk_mb_ai_actor FOREIGN KEY (triggered_by_user_id) REFERENCES mb_users(id) ON DELETE SET NULL');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_media_actor'),'SELECT 1','ALTER TABLE mb_media_analysis ADD CONSTRAINT fk_mb_media_actor FOREIGN KEY (actor_id) REFERENCES mb_users(id) ON DELETE RESTRICT');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;

-- @migration 008_internacionalizacao_turismo
CREATE TABLE IF NOT EXISTS mb_languages (
    code                     VARCHAR(16)  NOT NULL PRIMARY KEY,
    name                     VARCHAR(80)  NOT NULL,
    native_name              VARCHAR(80)  NOT NULL,
    flag_emoji               VARCHAR(16)  NOT NULL DEFAULT '',
    is_enabled               TINYINT(1)   NOT NULL DEFAULT 1,
    is_default               TINYINT(1)   NOT NULL DEFAULT 0,
    requires_human_approval  TINYINT(1)   NOT NULL DEFAULT 1,
    display_order            INT          NOT NULL DEFAULT 0,
    created_at               DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at               DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_content_translations (
    id                       BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    content_id               BIGINT UNSIGNED NOT NULL,
    source_language          VARCHAR(16)  NOT NULL DEFAULT 'pt-BR',
    target_language          VARCHAR(16)  NOT NULL,
    title                    VARCHAR(500) NOT NULL,
    summary                  VARCHAR(3000) NOT NULL DEFAULT '',
    body                     MEDIUMTEXT   NULL,
    cultural_notes           TEXT         NULL,
    provider                 VARCHAR(60)  NOT NULL DEFAULT 'Manual',
    model                    VARCHAR(100) NOT NULL DEFAULT '',
    version                  INT          NOT NULL DEFAULT 1,
    review_status            VARCHAR(24)  NOT NULL DEFAULT 'Pending',
    reviewed_by_id           BIGINT UNSIGNED NULL,
    reviewed_at              DATETIME(6)  NULL,
    review_notes             VARCHAR(1000) NULL,
    created_at               DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at               DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    KEY idx_ct_content_lang (content_id, target_language, review_status),
    KEY idx_ct_target_lang (target_language, review_status),
    CONSTRAINT fk_ct_content FOREIGN KEY (content_id) REFERENCES mb_content_items(id) ON DELETE CASCADE,
    CONSTRAINT fk_ct_reviewer FOREIGN KEY (reviewed_by_id) REFERENCES mb_users(id) ON DELETE SET NULL
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_place_translations (
    id                       BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    place_id                 BIGINT UNSIGNED NOT NULL,
    source_language          VARCHAR(16)  NOT NULL DEFAULT 'pt-BR',
    target_language          VARCHAR(16)  NOT NULL,
    name                     VARCHAR(300) NOT NULL,
    editorial_note           VARCHAR(2000) NULL,
    cultural_notes           TEXT         NULL,
    provider                 VARCHAR(60)  NOT NULL DEFAULT 'Manual',
    model                    VARCHAR(100) NOT NULL DEFAULT '',
    version                  INT          NOT NULL DEFAULT 1,
    review_status            VARCHAR(24)  NOT NULL DEFAULT 'Pending',
    reviewed_by_id           BIGINT UNSIGNED NULL,
    reviewed_at              DATETIME(6)  NULL,
    review_notes             VARCHAR(1000) NULL,
    created_at               DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at               DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    KEY idx_pt_place_lang (place_id, target_language, review_status),
    CONSTRAINT fk_pt_place FOREIGN KEY (place_id) REFERENCES mb_places(id) ON DELETE CASCADE,
    CONSTRAINT fk_pt_reviewer FOREIGN KEY (reviewed_by_id) REFERENCES mb_users(id) ON DELETE SET NULL
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_cultural_glossary (
    id                       BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    term                     VARCHAR(100) NOT NULL UNIQUE,
    category                 VARCHAR(60)  NOT NULL,
    explanation_pt           TEXT         NOT NULL,
    explanation_en           TEXT         NOT NULL,
    explanation_fr           TEXT         NOT NULL,
    explanation_es           TEXT         NOT NULL,
    explanation_ru           TEXT         NOT NULL,
    explanation_zh           TEXT         NOT NULL,
    explanation_ja           TEXT         NOT NULL,
    created_at               DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_tourist_itineraries (
    id                       BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    slug                     VARCHAR(120) NOT NULL UNIQUE,
    days                     INT          NOT NULL,
    theme                    VARCHAR(100) NOT NULL,
    title_pt                 VARCHAR(200) NOT NULL,
    title_en                 VARCHAR(200) NOT NULL,
    title_fr                 VARCHAR(200) NOT NULL,
    title_es                 VARCHAR(200) NOT NULL,
    title_ru                 VARCHAR(200) NOT NULL,
    title_zh                 VARCHAR(200) NOT NULL,
    title_ja                 VARCHAR(200) NOT NULL,
    summary_pt               VARCHAR(1000) NOT NULL,
    summary_en               VARCHAR(1000) NOT NULL,
    summary_fr               VARCHAR(1000) NOT NULL,
    summary_es               VARCHAR(1000) NOT NULL,
    summary_ru               VARCHAR(1000) NOT NULL,
    summary_zh               VARCHAR(1000) NOT NULL,
    summary_ja               VARCHAR(1000) NOT NULL,
    description_pt           TEXT         NOT NULL,
    description_en           TEXT         NOT NULL,
    description_fr           TEXT         NOT NULL,
    description_es           TEXT         NOT NULL,
    description_ru           TEXT         NOT NULL,
    description_zh           TEXT         NOT NULL,
    description_ja           TEXT         NOT NULL,
    display_order            INT          NOT NULL DEFAULT 0,
    is_published             TINYINT(1)   NOT NULL DEFAULT 1,
    created_at               DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_tourist_itinerary_stops (
    id                       BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    itinerary_id             BIGINT UNSIGNED NOT NULL,
    day_number               INT          NOT NULL DEFAULT 1,
    stop_order               INT          NOT NULL DEFAULT 1,
    place_id                 BIGINT UNSIGNED NULL,
    title_pt                 VARCHAR(200) NOT NULL,
    title_en                 VARCHAR(200) NOT NULL,
    period_of_day            VARCHAR(30)  NOT NULL DEFAULT 'Manhã',
    tip_pt                   VARCHAR(1000) NOT NULL,
    tip_en                   VARCHAR(1000) NOT NULL,
    tip_fr                   VARCHAR(1000) NOT NULL,
    tip_es                   VARCHAR(1000) NOT NULL,
    tip_ru                   VARCHAR(1000) NOT NULL,
    tip_zh                   VARCHAR(1000) NOT NULL,
    tip_ja                   VARCHAR(1000) NOT NULL,
    estimated_duration       VARCHAR(50)  NOT NULL DEFAULT '2 horas',
    CONSTRAINT fk_itinerary_stop_itin FOREIGN KEY (itinerary_id) REFERENCES mb_tourist_itineraries(id) ON DELETE CASCADE,
    CONSTRAINT fk_itinerary_stop_place FOREIGN KEY (place_id) REFERENCES mb_places(id) ON DELETE SET NULL
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- Seeds de linguagens ativas
INSERT IGNORE INTO mb_languages (code, name, native_name, flag_emoji, is_enabled, is_default, requires_human_approval, display_order) VALUES
 ('pt-BR', 'Português (Brasil)', 'Português', '🇧🇷', 1, 1, 0, 1),
 ('en', 'Inglês', 'English', '🇺🇸', 1, 0, 1, 2),
 ('fr', 'Francês', 'Français', '🇫🇷', 1, 0, 1, 3),
 ('es', 'Espanhol', 'Español', '🇪🇸', 1, 0, 1, 4),
 ('ru', 'Russo', 'Русский', '🇷🇺', 1, 0, 1, 5),
 ('zh-Hans', 'Chinês Simplificado', '简体中文', '🇨🇳', 1, 0, 1, 6),
 ('ja', 'Japonês', '日本語', '🇯🇵', 1, 0, 1, 7);

-- Seeds de permissoes de traducao
INSERT IGNORE INTO mb_permissions (code, description) VALUES
 ('translation.read', 'Ver idiomas, traduções e métricas de internacionalização'),
 ('translation.manage', 'Gerenciar, solicitar IA e aprovar traduções de conteúdo');

INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'translation.read' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'translation.manage' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';

INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'translation.read' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'translation.manage' FROM mb_roles WHERE normalized_name = 'EDITOR';

INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'translation.read' FROM mb_roles WHERE normalized_name = 'MODERATOR';

-- Seeds do Glossario Cultural Paraense / Amazonense
INSERT IGNORE INTO mb_cultural_glossary (term, category, explanation_pt, explanation_en, explanation_fr, explanation_es, explanation_ru, explanation_zh, explanation_ja) VALUES
 ('Ver-o-Peso', 'Patrimônio', 'Maior feira aberta da América Latina, às margens da Baía do Guajará, fundada no século XVII.', 'Largest open-air market in Latin America, on the banks of Guajará Bay, founded in the 17th century.', 'Le plus grand marché à ciel ouvert d''Amérique latine, au bord de la baie de Guajará, fondé au XVIIe siècle.', 'El mercado al aire libre más grande de América Latina, a orillas de la bahía de Guajará, fundado en el siglo XVII.', 'Крупнейший рынок под открытым небом в Латинской Америке, на берегу залива Гуажара, основан в XVII веке.', '拉丁美洲最大的露天市场，位于瓜哈拉湾畔，建于17世纪。', '17世紀に設立されたグアジャラ湾のほとりにあるラテンアメリカ最大の青空市場。'),
 ('Círio de Nazaré', 'Religião e Cultura', 'Uma das maiores manifestações religiosas e culturais do mundo, celebrada anualmente em outubro em Belém.', 'One of the world''s largest religious and cultural processions, celebrated every October in Belém.', 'L''une des plus grandes manifestations religieuses et culturelles au monde, célébrée chaque année en octobre à Belém.', 'Una de las mayores procesiones religiosas y culturales del mundo, celebrada cada octubre en Belém.', 'Одно из крупнейших религиозных и культурных шествий в мире, ежегодно отмечаемое в октябре в Белене.', '世界上规模最大的宗教文化盛会之一，每年十月在贝伦举行。', '世界最大級の宗教的・文化的な祝祭行列で、毎年10月にベレンで開催されます。'),
 ('Tacacá', 'Gastronomia', 'Iguaria amazônica quente servida em cuias com tucupi, goma de tapioca, jambu e camarão seco.', 'Traditional hot Amazonian delicacy served in gourds (cuias) made with tucupi broth, tapioca gum, jambu leaves, and dried shrimp.', 'Mets chaud traditionnel amazonien servi dans des calebasses, à base de tucupi, gomme de tapioca, jambu et crevettes séchées.', 'Manjar amazónico caliente servido en calabazas con caldo tucupí, goma de tapioca, hojas de jambú y camarones secos.', 'Традиционное горячее амазонское блюдо, подаваемое в калебасах: бульон тукупи, крахмал тапиоки, листья жамбу и сушеные креветки.', '传统的亚马逊热汤美食，盛在葫芦碗中，由图库皮汁、木薯粉糊、金纽扣叶（麻舌草）和干虾制成。', 'ひょうたんの器（クイア）で供される伝統的な熱いアマゾン料理。ツクピスープ、タピオカ澱粉糊、ジャンブーの葉、干しエビで作られます。'),
 ('Maniçoba', 'Gastronomia', 'Prato festivo feito com folhas de maniva moídas e cozidas por sete dias com carnes nobres de porco.', 'Festive ceremonial dish made with ground cassava leaves (maniva) boiled for seven days with smoked and cured meats.', 'Plat cérémoniel traditionnel préparé avec des feuilles de manioc cuites pendant sept jours et des viandes fumées.', 'Plato ceremonial festivo elaborado con hojas de yuca (maniva) cocidas durante siete días con carnes ahumadas.', 'Праздничное амазонское блюдо из листьев маниока (манива), которые варятся семь дней с копченым мясом.', '用磨碎的木薯叶（maniva）与熏肉连续熬煮七天制成的节庆传统名菜。', 'マニバ（キャッサバの葉）を7日間じっくり煮込み、燻製肉と合わせた祝祭の郷土料理。'),
 ('Açaí', 'Gastronomia', 'Fruto amazônico consumido puro com farinha de tapioca ou de mandioca e acompanhado de peixe frito.', 'Amazonian superfruit traditionally enjoyed pure and fresh with tapioca/manioc flour and fried river fish.', 'Superfruit amazonien traditionnellement dégusté pur et frais avec de la farine de manioc et du poisson frit.', 'Superfruta amazónica consumida tradicionalmente pura y fresca con harina de tapioca y pescado frito.', 'Амазонская ягода асаи, традиционно употребляемая в чистом виде с мукой тапиоки и жареной речной рыбой.', '亚马逊超级水果巴西莓，传统上新鲜浓稠食用，搭配木薯粉和油炸河鱼。', '新鮮なアサイー果肉をタピオカ粉やキャッサバ粉、揚げた川魚と一緒に食事として味わうアマゾンの伝統食。'),
 ('Carimbó', 'Música e Dança', 'Ritmo e dança folclórica de raízes indígenas e afro-brasileiras, Patrimônio Imaterial do Brasil.', 'Vibrant traditional rhythm and folk dance blending Indigenous Amazonian and Afro-Brazilian roots, Intangible Heritage of Brazil.', 'Rythme et danse folklorique dynamique aux racines indigènes et afro-brésiliennes, patrimoine immatériel du Brésil.', 'Ritmo y danza folclórica tradicional de raíces indígenas y afrobrasileñas, Patrimonio Cultural Inmaterial de Brasil.', 'Традиционный народный ритм и танец, объединяющий индейские амазонские и афробразильские корни.', '融合亚马逊原住民与非裔巴西传统的活力民俗节奏与舞蹈，巴西非物质文化遗产。', '先住民族とアフリカ系ブラジル人のルーツが融合した伝統舞踊・音楽で、ブラジルの無形文化遺産。'),
 ('Jambu', 'Botânica e Gastronomia', 'Erva típica da Amazônia que provoca uma sensação única e vibrante de formigamento e dormência nos lábios.', 'Amazonian toothache plant leaves producing a pleasant, electrifying tingling and numbing sensation on the tongue.', 'Herbe amazonienne typique qui procure une sensation pétillante et légèrement anesthésiante sur les lèvres.', 'Hierba amazónica que provoca una sensación vibrante de hormigueo y suave adormecimiento en la boca.', 'Амазонская пряная трава, вызывающая приятное покалывание и легкое онемение губ и языка.', '亚马逊特产草本植物（金纽扣），在舌尖和唇部产生独特的酥麻微颤感。', '口の中で心地よいピリピリとした痺れ感をもたらすアマゾン特有のハーブ（ジャンブー）。'),
 ('Tucupi', 'Gastronomia', 'Caldo amarelo brilhante extraído da raiz de mandioca-brava, fermentado e temperado com ervas da floresta.', 'Bright golden fermented broth extracted from wild manioc root, seasoned with chicória and alfavaca.', 'Bouillon doré fermenté extrait de la racine de manioc sauvage, aromatisé aux herbes de la forêt.', 'Caldo amarillo brillante fermentado extraído de la raíz de yuca brava, sazonado con hierbas amazónicas.', 'Ярко-желтый ферментированный бульон из корня дикого маниока, приправленный лесными травами.', '由野生木薯根榨汁发酵、配以雨林香草熬制而成的金黄色风味汤汁。', '野生キャッサバの搾り汁を発酵させ、アマゾンのハーブで煮込んだ黄金色の伝統スープ。');

-- Seeds dos Roteiros Turisticos "Explore Belém"
INSERT IGNORE INTO mb_tourist_itineraries (slug, days, theme, title_pt, title_en, title_fr, title_es, title_ru, title_zh, title_ja, summary_pt, summary_en, summary_fr, summary_es, summary_ru, summary_zh, summary_ja, description_pt, description_en, description_fr, description_es, description_ru, description_zh, description_ja, display_order) VALUES
 ('belem-express-1-dia', 1, 'Express', 'Belém Express — O Essencial em 1 Dia', 'Belém Express — The Essentials in 1 Day', 'Belém Express — L''Essentiel en 1 Jour', 'Belém Express — Lo Esencial en 1 Día', 'Белен Экспресс — Главное за 1 день', '贝伦精粹 — 1日游', 'ベレン・エクスプレス — 1日凝縮プラン',
 'Para quem tem pouco tempo e quer vivenciar o coração histórico, gastronômico e cultural de Belém.',
 'For visitors with limited time wanting to experience the historic, culinary, and cultural heart of Belém.',
 'Pour les visiteurs disposant de peu de temps qui souhaitent découvrir le cœur historique et gastronomique de Belém.',
 'Para quienes disponen de poco tiempo y quieren vivir el corazón histórico y gastronómico de Belém.',
 'Для тех, у кого мало времени, но есть желание ощутить историческое и кулинарное сердце Белена.',
 '适合时间紧凑、希望体验贝伦历史与美食核心的旅行者。',
 '時間がない中でもベレンの歴史・グルメ・文化の核心を味わいたい方に最適。',
 'Comece o dia cedo no Ver-o-Peso assistindo à chegada das embarcações de açaí. Caminhe pelo Complexo Feliz Lusitânia e Forte do Presépio. Almoce no Mercado de Peixe ou na Estação das Docas. À tarde, encante-se com o Theatro da Paz e termine o pôr do sol no Mangal das Garças com a Revoada dos Guarás.',
 'Start early at Ver-o-Peso market watching açaí boats arrive. Walk to Feliz Lusitânia complex and Forte do Presépio. Enjoy lunch at the Fish Market or Estação das Docas. In the afternoon, tour Theatro da Paz and catch the sunset at Mangal das Garças watching the scarlet ibises return.',
 'Commencez tôt au marché Ver-o-Peso. Visitez le complexe historique Feliz Lusitânia et le Forte do Presépio. Déjeunez à Estação das Docas. L''après-midi, admirez le Theatro da Paz et terminez au coucher du soleil à Mangal das Garças.',
 'Comience temprano en el Ver-o-Peso. Recorra el complejo Feliz Lusitânia y el Forte do Presépio. Almuerce en Estação das Docas. Por la tarde, admire el Theatro da Paz y disfrute de la puesta de sol en Mangal das Garças.',
 'Начните утро на рынке Вер-о-Пезо. Прогуляйтесь по комплексу Фелиз Лузитания и форту Пресепио. Пообедайте на Эстасан-дас-Докас. Днем посетите Театро да Паз и встретьте закат в Мангал-дас-Гарсас.',
 '清晨漫步韦尔奥佩索市场，游览费利斯卢西塔尼亚建筑群与堡垒，午餐享用码头港湾美食，下午探访和平剧院，傍晚在鹭鸟公园欣赏红鹮归巢与落日。',
 '早朝のベル・オ・ペーゾ市場からスタートし、歴史地区フェリス・ルジタニア、午後は平和劇場、夕暮れはマンガル・ダス・ガルサスで緋色のショウジョウトキの帰巣を鑑賞します。',
 1),
 ('belem-cultural-2-dias', 2, 'Cultural', 'Belém Cultural e Ilha do Combu — 2 Dias', 'Belém Cultural & Combu Island — 2 Days', 'Belém Culturelle & Île de Combu — 2 Jours', 'Belém Cultural e Isla de Combu — 2 Días', 'Культурный Белен и остров Комбу — 2 дня', '贝伦文化与孔布岛 — 2日游', '文化のベレンとコンブー島 — 2日間',
 'Dois dias combinando os monumentos urbanos e a rica experiência ribeirinha na Ilha do Combu.',
 'Two days combining urban architecture, museums, and an authentic Amazonian river island boat excursion.',
 'Deux jours alliant architecture urbaine, musées et une escapade authentique sur l''île fluviale de Combu.',
 'Dos días combinando arquitectura colonial, museos y una experiencia ribereña en la Isla de Combu.',
 'Два дня, сочетающие колониальные памятники, музеи и водную прогулку на речной остров Комбу.',
 '两天时间融合城市古典建筑、生态博物馆与孔布岛雨林原生态水上风情。',
 '都市の歴史的建造物と、川を渡るコンブー島の素朴なアマゾン島旅を満喫する2日間。',
 'O Dia 1 cobre o centro histórico, Basílica de Nazaré e Theatro da Paz. O Dia 2 convida você a cruzar o Rio Guamá de barco até a Ilha do Combu para saborear peixe assado na brasa, conhecer o cultivo orgânico de cacau artesanal e relaxar à beira do rio.',
 'Day 1 covers the historic center, Basilica of Our Lady of Nazareth, and Theatro da Paz. Day 2 invites you across Guamá river by wooden boat to Combu Island for grilled fresh fish, bean-to-bar artisanal Amazon chocolate, and river bathing.',
 'Jour 1: centre historique, Basilique de Nazareth et Theatro da Paz. Jour 2: traversée en bateau vers l''île de Combu, dégustation de poisson grillé et chocolat artisanal d''Amazonie.',
 'Día 1: centro histórico, Basílica de Nazaret y Theatro da Paz. Día 2: travesía en lancha a la Isla de Combu, pescado a la brasa y chocolate artesanal de cacao amazónico.',
 'День 1: исторический центр, Базилика Назаре и Театро да Паз. День 2: лодочная прогулка на остров Комбу, жареная речная рыба и ремесленный амазонский шоколад.',
 '第一天探访历史街区、拿撒勒圣殿与大剧院；第二天乘木船横渡瓜玛河前往孔布岛，品尝炭烤河鱼与纯手工亚马逊黑巧。',
 '1日目は歴史街区とナザレ聖堂・平和劇場。2日目はボートでコンブー島へ渡り、川魚の炭火焼きや職人カカオチョコを堪能します。',
 2),
 ('belem-natureza-3-dias', 3, 'Natureza', 'Belém Natureza, Rios e Tradição — 3 Dias', 'Belém Nature, Rivers & Heritage — 3 Days', 'Belém Nature, Rivières & Patrimoine — 3 Jours', 'Belém Naturaleza, Ríos y Tradición — 3 Días', 'Природа Белена, реки и наследие — 3 дня', '贝伦自然生态、江流与传统 — 3日游', '自然と川と伝統のベレン — 3日間',
 'Três dias com trilhas na selva urbana do Utinga, orquídeas no Museu Goeldi e artesanato marajoara no Polo Joalheiro.',
 'Three days featuring trails at Utinga State Park, biodiversity at Emílio Goeldi Museum, and Marajoara ceramics at São José Liberto.',
 'Trois jours avec sentiers au parc d''Utinga, biodiversité au Musée Goeldi et artisanat marajoara au Polo Joalheiro.',
 'Tres días con senderos en el Parque Utinga, biodiversidad en el Museo Goeldi y cerámica marajoara en el Polo Joyero.',
 'Три дня с тропами в парке Утинга, фауной в Музее Эмилио Гоелди и маражоарской керамикой в Сан-Жозе-Либерто.',
 '三天深度体验乌廷加雨林国家公园绿道骑行、戈埃尔迪百年自然博物馆与圣何塞珠宝工艺中心。',
 'ウチンガ州立公園のサイクリング、ゲウジ自然科学博物館、サン・ジョゼ・リベルトの宝石・マラジョ土器をめぐる3日間。',
 'Dias 1 e 2 com o melhor do centro histórico e Ilha do Combu. O Dia 3 começa pedalando nas trilhas do Parque Estadual do Utinga cercado pela floresta primária, segue com visita ao centenário Museu Paraense Emílio Goeldi e finaliza no Polo Joalheiro Espaço São José Liberto conhecendo a arte cerâmica marajoara e joias com gemas da Amazônia.',
 'Days 1 and 2 cover the highlights and Combu Island. Day 3 features cycling the protected trails of Utinga State Park surrounded by rainforest, visiting the famous 19th-century Emílio Goeldi Museum, and discovering ancient Marajoara ceramics at São José Liberto.',
 'Jours 1 et 2: les incontournables et l''île de Combu. Jour 3: randonnée ou vélo au Parc d''Utinga, visite du Musée Emílio Goeldi et découvertes d''artisanat au Polo Joalheiro.',
 'Días 1 y 2: lo mejor del centro e Isla de Combu. Día 3: ciclismo en el Parque Utinga, fauna amazónica en el Museo Goeldi y joyas y cerámica en San José Liberto.',
 'Дни 1 и 2: знаковые места и остров Комбу. День 3: прогулка по парку Утинга, старейший музей Эмилио Гоелди и керамика маражоара в Сан-Жозе-Либерто.',
 '第1至2天囊括城市核心景点与孔布岛；第3天清晨前往乌廷加湿地雨林公园骑行，午后造访百年艾米利奥戈埃尔迪生态博物馆，傍晚前往圣何塞珠宝工坊欣赏古代马拉若陶艺。',
 '1・2日目に加え、3日目はウチンガ州立公園の原生林トレイル、歴史あるエミリオ・ゲウジ博物館、サン・ジョゼ・リベルトでのマラジョ土器・宝石工芸鑑賞。',
 3),
 ('belem-imersao-5-dias', 5, 'Completo', 'Imersão Amazônica Completa — 5 Dias', 'Complete Amazonian Immersion — 5 Days', 'Immersion Amazonienne Complète — 5 Jours', 'Inmersión Amazónica Completa — 5 Días', 'Полное погружение в Амазонию — 5 дней', '亚马逊全景深度体验 — 5日游', 'アマゾン完全周遊プラン — 5日間',
 'O roteiro definitivo: história, gastronomia, ilhas com praias de água doce com ondas, Icoaraci e florestas protegidas.',
 'The ultimate itinerary: history, cuisine, freshwater beach islands with river waves in Mosqueiro, pottery masters in Icoaraci, and deep rainforest immersion.',
 'L''itinéraire ultime: histoire, haute gastronomie, plages d''eau douce à vagues à Mosqueiro, maîtres potiers à Icoaraci et forêt préservée.',
 'El itinerario definitivo: patrimonio histórico, gastronomía, playas de agua dulce con olas en Mosqueiro y cerámica en Icoaraci.',
 'Самый полный маршрут: история, кухня, пресноводные пляжи Москейро с речными волнами, керамика Икоараси и дикая природа.',
 '终极全景体验：历史古迹、舌尖美食、莫斯凯罗淡水浪涌海滩、伊科阿拉西古法马拉若陶工坊与原生雨林。',
 '決定版プラン：歴史、郷土料理、モスケイロ島の淡水白砂ビーチ、イコアラシ陶芸の里、緑豊かな保護区を巡る完全旅程。',
 'Cinco dias para se apaixonar por Belém. Inclui todo o patrimônio histórico, gastronomia ribeirinha no Combu, natureza no Utinga e Bosque Rodrigues Alves, dia de praia doce de rio com ondas na bucólica Ilha de Mosqueiro, e vivência com mestres artesãos do barro em Icoaraci no Paracuri.',
 'Five days to fall in love with Belém. Includes all historical landmarks, riverside Combu food tour, Utinga rainforest trails, a full day at Mosqueiro Island relaxing on wide freshwater beaches with waves, and watching master ceramicists sculpt Marajoara urns in Icoaraci.',
 'Cinq jours pour aimer Belém: centre historique, île de Combu, parc d''Utinga, une journée sur les plages d''eau douce de l''île de Mosqueiro et ateliers de poterie marajoara à Icoaraci.',
 'Cinco días inolvidables: monumentos coloniales, Combu, parque Utinga, día de playa de río en la pintoresca Isla de Mosqueiro y visita a los talleres alfareros de Icoaraci.',
 'Пять незабываемых дней: памятники архитектуры, остров Комбу, парк Утинга, пляжи острова Москейро и традиционные гончарные мастерские Икоараси.',
 '五天沉浸之旅：涵盖老城全部名胜、孔布岛河鲜之宴、乌廷加绿道、莫斯凯罗岛浩瀚淡水沙滩逐浪以及伊科阿拉西马拉若传统陶艺家工坊。',
 '5日間のベレン満喫旅：歴史遺産、コンブー島、ウチンガ公園、川波の寄せるモスケイロ島の淡水ビーチ、イコアラシ陶芸村の職人技見学。',
 4);

-- @migration 009_mobilidade_comunidade_parceiros
CREATE TABLE IF NOT EXISTS mb_transit_lines (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  code VARCHAR(30) NOT NULL,
  name VARCHAR(200) NOT NULL,
  origin VARCHAR(150) NOT NULL,
  destination VARCHAR(150) NOT NULL,
  fare DECIMAL(10,2) NOT NULL DEFAULT 4.00,
  transit_type VARCHAR(50) NOT NULL DEFAULT 'Urbano',
  operator_name VARCHAR(150) NULL,
  has_air_conditioning BOOLEAN NOT NULL DEFAULT FALSE,
  has_wheelchair_lift BOOLEAN NOT NULL DEFAULT TRUE,
  itinerary_summary TEXT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uq_transit_line_code (code),
  KEY idx_transit_type (transit_type)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_transit_stops (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  name VARCHAR(200) NOT NULL,
  address VARCHAR(300) NULL,
  neighborhood VARCHAR(120) NOT NULL,
  municipality VARCHAR(120) NOT NULL DEFAULT 'Belém',
  latitude DOUBLE NULL,
  longitude DOUBLE NULL,
  stop_type VARCHAR(50) NOT NULL DEFAULT 'Parada',
  is_accessible BOOLEAN NOT NULL DEFAULT TRUE,
  lines_serviced VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_transit_stop_neighborhood (neighborhood)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_transit_alerts (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  title VARCHAR(250) NOT NULL,
  description TEXT NOT NULL,
  neighborhood VARCHAR(120) NULL,
  severity VARCHAR(30) NOT NULL DEFAULT 'Media',
  alert_type VARCHAR(50) NOT NULL DEFAULT 'Obras',
  start_at DATETIME NOT NULL,
  end_at DATETIME NULL,
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  created_by_user_id BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_transit_alerts_active (is_active, start_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_river_routes (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  name VARCHAR(200) NOT NULL,
  departure_port VARCHAR(200) NOT NULL,
  destination_port VARCHAR(200) NOT NULL,
  destination_island VARCHAR(100) NOT NULL,
  estimated_duration_minutes INT NOT NULL DEFAULT 15,
  fare DECIMAL(10,2) NOT NULL DEFAULT 10.00,
  vessel_type VARCHAR(80) NOT NULL DEFAULT 'Popopô / Barco Regional',
  operating_hours VARCHAR(250) NOT NULL DEFAULT 'Diário das 07h às 19h',
  operator_notes TEXT NULL,
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_river_destination (destination_island)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_neighborhood_guides (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  name VARCHAR(120) NOT NULL,
  slug VARCHAR(120) NOT NULL,
  zone VARCHAR(50) NOT NULL,
  history_summary TEXT NOT NULL,
  cultural_characteristics TEXT NOT NULL,
  safety_tips TEXT NULL,
  popular_attractions TEXT NULL,
  display_order INT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_neighborhood_slug (slug),
  KEY idx_neighborhood_zone (zone)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_campaign_metrics (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  campaign_id BIGINT NOT NULL,
  metric_date DATE NOT NULL,
  impressions INT NOT NULL DEFAULT 0,
  clicks INT NOT NULL DEFAULT 0,
  category VARCHAR(50) NULL,
  neighborhood VARCHAR(120) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_campaign_metric (campaign_id, metric_date, category, neighborhood),
  KEY idx_metric_date (metric_date)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Seeds de Mobilidade Urbana Paraense (Linhas expressas, BRT e travessias fluviais)
INSERT IGNORE INTO mb_transit_lines (code, name, origin, destination, fare, transit_type, operator_name, has_air_conditioning, has_wheelchair_lift, itinerary_summary)
VALUES
 ('BRT-01', 'BRT Expresso Maracangalha — São Brás', 'Terminal Maracangalha', 'Terminal São Brás', 4.00, 'BRT', 'Consórcio Sistema Integrado Belém', TRUE, TRUE, 'Canal exclusivo da Av. Augusto Montenegro e Almirante Barroso.'),
 ('LINE-226', 'Maró / Pedreira — Presidente Vargas', 'Pedreira', 'Centro / Ver-o-Peso', 4.00, 'Urbano', 'Viação Forte', FALSE, TRUE, 'Atende Pedro Miranda, Senador Lemos, Presidente Vargas e Ver-o-Peso.'),
 ('LINE-758', 'Satélite — Pte. Vargas', 'Conjunto Satélite', 'Praça da República', 4.00, 'Urbano', 'Guajará Transportes', TRUE, TRUE, 'Via Mário Covas, Almirante Barroso e Av. Presidente Vargas.'),
 ('RIV-01', 'Travessia Fluvial Praça do Pescador — Ilha do Combu', 'Porto da Praça do Pescador (Ver-o-Peso)', 'Furos e Restaurantes do Combu', 12.00, 'Fluvial', 'Cooperativa dos Barqueiros do Combu (Coopbarco)', FALSE, TRUE, 'Acesso aos restaurantes ribeirinhos e produção de cacau/chocolate artesanal.'),
 ('RIV-02', 'Travessia Trapiche de Icoaraci — Ilha de Cotijuba', 'Trapiche de Icoaraci', 'Ilha de Cotijuba (Praia do Vai-Quem-Quer)', 10.00, 'Fluvial', 'Navio Municipal SEMOB / Barcos Particulares', FALSE, TRUE, 'Transporte diário com saídas regulares pela manhã e tarde.');

-- Seeds de Bairros de Belém ("Meu Bairro")
INSERT IGNORE INTO mb_neighborhood_guides (name, slug, zone, history_summary, cultural_characteristics, safety_tips, popular_attractions, display_order)
VALUES
 ('Cidade Velha', 'cidade-velha', 'Centro Histórico', 'Berço da fundação de Belém em 1616 por Francisco Caldeira Castelo Branco ao erguer o Forte do Presépio.', 'Ruas de pedra, casarões azulejados portugueses, culinária ribeirinha e festas de carimbó.', 'Atenção aos pertences à noite em ruas desertas.', 'Forte do Presépio, Catedral da Sé, Casa das Onze Janelas, Museu de Arte Sacra.', 1),
 ('Campina / Comércio', 'campina', 'Centro Histórico', 'Primeiro bairro comercial de Belém, famoso pelas feiras de rua e arquitetura do Ciclo da Borracha.', 'Comércio vibrante, bancas de ervas aromáticas, garrafadas de cheiro do Pará e artesanato miriti.', 'Mantenha bolsas à frente e celulares guardados ao caminhar nas aglomerações da feira.', 'Ver-o-Peso, Praça do Relógio, Solar da Baronesa, Igreja de Sant''Ana.', 2),
 ('Nazaré', 'nazare', 'Região Central', 'Coração da fé católica paraense, onde converge o Círio de Nazaré todo segundo domingo de outubro.', 'Túneis de mangueiras centenárias, cafés modernos, praças floridas e a grandiosa Basílica.', 'Bairro com boa iluminação e policiamento turístico permanente.', 'Basílica Santuário de Nazaré, Praça Santuário, Museu Memória de Nazaré.', 3),
 ('Umarizal', 'umarizal', 'Zona Sul', 'Antiga área de olarias e trapiches, hoje o principal polo gastronômico e boêmio moderno da capital.', 'Bares sofisticados, restaurantes de alta gastronomia amazônica contemporânea e edifícios modernos.', 'Muito movimentado à noite; utilize serviços de transporte ou valets credenciados.', 'Polo Gastronômico da Doca, Praça Brasil, Casa Cor Pará.', 4),
 ('Batista Campos', 'batista-campos', 'Região Central', 'Área residencial nobre e tradicional, célebre pela sua praça homônima premiada nacionalmente.', 'Clima bucólico familiar, quiosques de água de coco, sorveterias típicas e passeios ao amanhecer.', 'Bairro tranquilo, ideal para caminhadas diurnas e piqueniques na praça.', 'Praça Batista Campos (Castelinhos e lagos com vitórias-régias).', 5);

-- @migration 010_radar_autonomo_descoberta
-- SPRINT 08: MinhaBelém Radar - Robô autônomo de descoberta de informações e eventos
CREATE TABLE IF NOT EXISTS mb_radar_discovered_sources (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    source_name VARCHAR(250) NOT NULL,
    url VARCHAR(1500) NOT NULL,
    url_hash BINARY(32) NOT NULL UNIQUE,
    category VARCHAR(100) NOT NULL DEFAULT 'geral',
    municipality VARCHAR(100) NOT NULL DEFAULT 'Belém',
    discovered_from_url VARCHAR(1500) NULL,
    evaluation_status ENUM('pending', 'approved', 'rejected') NOT NULL DEFAULT 'pending',
    evaluation_notes TEXT NULL,
    feed_protocol_candidate ENUM('rss', 'atom', 'html', 'jsonld', 'ical', 'sitemap') NOT NULL DEFAULT 'html',
    confidence_score DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    discovered_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    evaluated_at DATETIME(6) NULL,
    evaluated_by_id BIGINT NULL,
    INDEX idx_radar_disc_status (evaluation_status, discovered_at),
    INDEX idx_radar_disc_cat (category, municipality)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_radar_crawl_history (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    source_id BIGINT NULL,
    url VARCHAR(1500) NOT NULL,
    url_hash BINARY(32) NOT NULL,
    status_code INT NOT NULL DEFAULT 0,
    etag VARCHAR(200) NULL,
    last_modified DATETIME(6) NULL,
    content_hash CHAR(64) NULL,
    discovered_items_count INT NOT NULL DEFAULT 0,
    events_identified_count INT NOT NULL DEFAULT 0,
    depth INT NOT NULL DEFAULT 0,
    duration_ms INT NOT NULL DEFAULT 0,
    error_message TEXT NULL,
    crawled_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    INDEX idx_radar_crawl_source (source_id, crawled_at),
    INDEX idx_radar_crawl_url (url_hash, crawled_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_radar_social_profiles (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    platform ENUM('instagram', 'facebook', 'tiktok', 'youtube') NOT NULL,
    profile_handle VARCHAR(150) NOT NULL,
    display_name VARCHAR(200) NOT NULL,
    profile_url VARCHAR(1000) NOT NULL,
    official_embed_url VARCHAR(1500) NULL,
    category VARCHAR(100) NOT NULL DEFAULT 'cultura',
    municipality VARCHAR(100) NOT NULL DEFAULT 'Belém',
    license_notes VARCHAR(500) NOT NULL DEFAULT 'Conteúdo público oficial para divulgação cultural',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    last_checked_at DATETIME(6) NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UNIQUE KEY uk_platform_handle (platform, profile_handle),
    INDEX idx_radar_social_active (is_active, platform)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Seeds de Perfis Oficiais e Canais Públicos de Belém e Pará (Divulgação Cultural & Eventos)
INSERT IGNORE INTO mb_radar_social_profiles (platform, profile_handle, display_name, profile_url, official_embed_url, category, municipality, license_notes, is_active)
VALUES
 ('instagram', 'theatrodapazoficial', 'Theatro da Paz (Secult/PA)', 'https://www.instagram.com/theatrodapazoficial/', 'https://www.instagram.com/theatrodapazoficial/embed', 'teatro', 'Belém', 'Programação teatral e concertos públicos oficiais.', TRUE),
 ('instagram', 'secultpara', 'Secretaria de Cultura do Pará', 'https://www.instagram.com/secultpara/', 'https://www.instagram.com/secultpara/embed', 'cultura', 'Belém', 'Avisos e editais da cultura paraense.', TRUE),
 ('instagram', 'fumbel.belem', 'Fundação Cultural de Belém (FUMBEL)', 'https://www.instagram.com/fumbel.belem/', 'https://www.instagram.com/fumbel.belem/embed', 'cultura', 'Belém', 'Editais, festivais e eventos municipais de Belém.', TRUE),
 ('instagram', 'estacaodasdocas', 'Estação das Docas (Pará 2000)', 'https://www.instagram.com/estacaodasdocas/', 'https://www.instagram.com/estacaodasdocas/embed', 'turismo', 'Belém', 'Música ao vivo, feiras gastronômicas e pôr do som.', TRUE),
 ('instagram', 'museugoeldi', 'Museu Paraense Emílio Goeldi', 'https://www.instagram.com/museugoeldi/', 'https://www.instagram.com/museugoeldi/embed', 'educacao', 'Belém', 'Exposições científicas e visitas educativas.', TRUE),
 ('youtube', 'AgenciaBelem', 'Agência Belém de Notícias', 'https://www.youtube.com/@AgenciaBelem', 'https://www.youtube.com/embed?listType=user_uploads&list=AgenciaBelem', 'servicos', 'Belém', 'Coberturas oficiais e comunicados de trânsito/obras.', TRUE),
 ('youtube', 'governopara', 'Governo do Estado do Pará', 'https://www.youtube.com/@governopara', 'https://www.youtube.com/embed?listType=user_uploads&list=governopara', 'servicos', 'Belém', 'Transmissões oficiais e serviços de saúde/segurança.', TRUE);

-- Fontes públicas pré-catalogadas no Radar para descoberta e ingestão
INSERT IGNORE INTO mb_radar_discovered_sources (source_name, url, url_hash, category, municipality, discovered_from_url, evaluation_status, evaluation_notes, feed_protocol_candidate, confidence_score)
VALUES
 ('Agência Belém - Notícias Oficiais', 'https://agenciabelem.com.br/Noticias', UNHEX(SHA2('https://agenciabelem.com.br/Noticias', 256)), 'noticias', 'Belém', 'https://belem.pa.gov.br', 'approved', 'Portal oficial de notícias do município de Belém.', 'html', 0.95),
 ('Secult Pará - Agenda Cultural', 'https://secult.pa.gov.br/agenda', UNHEX(SHA2('https://secult.pa.gov.br/agenda', 256)), 'cultura', 'Belém', 'https://pa.gov.br', 'approved', 'Agenda de espetáculos e editais culturais do Estado.', 'jsonld', 0.98),
 ('Fundação Cultural do Pará - Centur', 'https://fcp.pa.gov.br/eventos', UNHEX(SHA2('https://fcp.pa.gov.br/eventos', 256)), 'cultura', 'Belém', 'https://pa.gov.br', 'approved', 'Programação de cinema, teatro e oficinas do Centur.', 'html', 0.92),
 ('Theatro da Paz - Programação', 'https://theatrodapaz.com.br/programacao', UNHEX(SHA2('https://theatrodapaz.com.br/programacao', 256)), 'teatro', 'Belém', 'https://secult.pa.gov.br', 'approved', 'Espetáculos da Temporada de Ópera e concertos da OSTP.', 'html', 0.95),
 ('Portal Agência Pará', 'https://agenciapara.com.br/noticias', UNHEX(SHA2('https://agenciapara.com.br/noticias', 256)), 'noticias', 'Belém', 'https://pa.gov.br', 'pending', 'Canal oficial de notícias e serviços públicos estaduais.', 'html', 0.88);



