-- =====================================================================
--  GESTIÓN DE TURNOS — RESPALDO COMPLETO (ESTRUCTURA + DATOS)
--  Generado desde Supabase el 2026-11-18 con pg_dump (PostgreSQL 17).
--
--  Incluye: esquemas asistencia, auditoria, novedades, organizacion,
--  parametrizacion, seguridad, turnos y public ("DataProtectionKeys"),
--  con tablas, índices, vistas, funciones, triggers y todos los datos.
--  No incluye: los datos de seguridad.sesion_dispositivo (refresh tokens).
--
--  Uso: en una base VACÍA (proyecto Supabase nuevo) → SQL Editor →
--  pegar el archivo completo → Run. O bien:  psql "<cadena>" -f este_archivo.sql
--  Todo corre en una sola transacción: si algo falla no queda nada a medias.
--
--  ⚠ Contiene datos reales (correos, hashes de contraseñas, auditoría).
--    No lo subas a un repositorio público.
-- =====================================================================

BEGIN;

-- Extensiones (en Supabase ya existen en el esquema "extensions"; en otro servidor se crean)
CREATE SCHEMA IF NOT EXISTS extensions;
CREATE EXTENSION IF NOT EXISTS btree_gist WITH SCHEMA extensions;   -- restricción ex_vacaciones_sin_cruce
CREATE EXTENSION IF NOT EXISTS pgcrypto WITH SCHEMA extensions;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp" WITH SCHEMA extensions;

--
-- PostgreSQL database dump
--


-- Dumped from database version 17.12
-- Dumped by pg_dump version 18.6

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: asistencia; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA IF NOT EXISTS asistencia;


--
-- Name: auditoria; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA IF NOT EXISTS auditoria;


--
-- Name: novedades; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA IF NOT EXISTS novedades;


--
-- Name: organizacion; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA IF NOT EXISTS organizacion;


--
-- Name: parametrizacion; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA IF NOT EXISTS parametrizacion;


--
-- Name: public; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA IF NOT EXISTS public;


--
-- Name: SCHEMA public; Type: COMMENT; Schema: -; Owner: -
--



--
-- Name: seguridad; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA IF NOT EXISTS seguridad;


--
-- Name: turnos; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA IF NOT EXISTS turnos;


--
-- Name: fn_bitacora_inmutable(); Type: FUNCTION; Schema: auditoria; Owner: -
--

CREATE FUNCTION auditoria.fn_bitacora_inmutable() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    RAISE EXCEPTION 'La bitácora de auditoría no admite modificaciones ni borrados';
END;
$$;


--
-- Name: fn_registrar_cambio(); Type: FUNCTION; Schema: auditoria; Owner: -
--

CREATE FUNCTION auditoria.fn_registrar_cambio() RETURNS trigger
    LANGUAGE plpgsql SECURITY DEFINER
    SET search_path TO 'pg_catalog', 'pg_temp'
    AS $$
DECLARE
    v_pk       text  := TG_ARGV[0];
    v_anterior jsonb;
    v_nuevo    jsonb;
    v_fila     jsonb;
    v_accion   text;
    v_dif_ant  jsonb;
    v_dif_nue  jsonb;
BEGIN
    IF TG_OP = 'INSERT' THEN
        v_nuevo  := to_jsonb(NEW) - 'hash_contrasena';
        v_fila   := v_nuevo;
        v_accion := 'CREAR';
        v_dif_nue := v_nuevo;
    ELSIF TG_OP = 'UPDATE' THEN
        v_anterior := to_jsonb(OLD) - 'hash_contrasena';
        v_nuevo    := to_jsonb(NEW) - 'hash_contrasena';
        v_fila     := v_nuevo;
        v_accion   := 'MODIFICAR';
        SELECT jsonb_object_agg(a.key, a.value), jsonb_object_agg(a.key, v_nuevo -> a.key)
          INTO v_dif_ant, v_dif_nue
          FROM jsonb_each(v_anterior) a
         WHERE (v_nuevo -> a.key) IS DISTINCT FROM a.value;
        IF v_dif_ant IS NULL THEN
            RETURN NEW;                           -- no cambió nada relevante
        END IF;
    ELSE
        v_anterior := to_jsonb(OLD) - 'hash_contrasena';
        v_fila     := v_anterior;
        v_accion   := 'ELIMINAR';
        v_dif_ant  := v_anterior;
    END IF;

    INSERT INTO auditoria.bitacora_negocio
        (id_usuario, cod_tipo_accion, esquema, tabla, id_registro,
         id_empleado, valor_anterior, valor_nuevo, ip_origen)
    VALUES
        (NULLIF(current_setting('app.id_usuario', true), '')::bigint,
         v_accion, TG_TABLE_SCHEMA, TG_TABLE_NAME, v_fila ->> v_pk,
         (v_fila ->> 'id_empleado')::bigint,
         v_dif_ant, v_dif_nue,
         NULLIF(current_setting('app.ip', true), '')::inet);

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$;


--
-- Name: fn_validar_asignacion(); Type: FUNCTION; Schema: turnos; Owner: -
--

CREATE FUNCTION turnos.fn_validar_asignacion() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NEW.cod_estado_asignacion = 'ANULADA' THEN
        RETURN NEW;
    END IF;

    IF EXISTS (SELECT 1 FROM novedades.solicitud_vacaciones v
                WHERE v.id_empleado = NEW.id_empleado
                  AND v.cod_estado_solicitud = 'APROBADA'
                  AND NEW.fecha BETWEEN v.fecha_inicio AND v.fecha_fin) THEN
        RAISE EXCEPTION 'El empleado tiene vacaciones aprobadas en la fecha %', NEW.fecha
              USING ERRCODE = 'check_violation';
    END IF;

    IF EXISTS (SELECT 1 FROM novedades.novedad n
                WHERE n.id_empleado = NEW.id_empleado
                  AND n.cod_estado_solicitud = 'APROBADA'
                  AND tstzrange(n.inicio, n.fin) && NEW.periodo) THEN
        RAISE EXCEPTION 'El empleado tiene un permiso o ausencia aprobada que se cruza con el turno'
              USING ERRCODE = 'check_violation';
    END IF;

    IF EXISTS (SELECT 1 FROM turnos.dia_descanso d
                WHERE d.id_empleado = NEW.id_empleado AND d.fecha = NEW.fecha) THEN
        RAISE EXCEPTION 'El empleado tiene día de descanso programado el %', NEW.fecha
              USING ERRCODE = 'check_violation';
    END IF;

    RETURN NEW;
END;
$$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: detalle_horas; Type: TABLE; Schema: asistencia; Owner: -
--

CREATE TABLE asistencia.detalle_horas (
    id_registro bigint NOT NULL,
    cod_categoria_hora character varying(30) NOT NULL,
    minutos integer NOT NULL,
    CONSTRAINT detalle_horas_minutos_check CHECK ((minutos > 0))
);
ALTER TABLE ONLY asistencia.detalle_horas ALTER COLUMN id_registro SET (pg_attver=169635952);
ALTER TABLE ONLY asistencia.detalle_horas ALTER COLUMN cod_categoria_hora SET (pg_attver=169635952);
ALTER TABLE ONLY asistencia.detalle_horas ALTER COLUMN minutos SET (pg_attver=169635952);


--
-- Name: registro; Type: TABLE; Schema: asistencia; Owner: -
--

CREATE TABLE asistencia.registro (
    id_registro bigint NOT NULL,
    id_empleado bigint NOT NULL,
    id_asignacion bigint,
    hora_entrada timestamp with time zone NOT NULL,
    hora_salida timestamp with time zone,
    cod_fuente_marcacion character varying(20) DEFAULT 'WEB'::character varying NOT NULL,
    observacion character varying(300),
    registrado_por bigint,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    salida_no_registrada boolean DEFAULT false NOT NULL,
    CONSTRAINT registro_check CHECK (((hora_salida IS NULL) OR (hora_salida > hora_entrada)))
);
ALTER TABLE ONLY asistencia.registro ALTER COLUMN id_registro SET (pg_attver=169589768);
ALTER TABLE ONLY asistencia.registro ALTER COLUMN id_empleado SET (pg_attver=169589768);
ALTER TABLE ONLY asistencia.registro ALTER COLUMN id_asignacion SET (pg_attver=169589768);
ALTER TABLE ONLY asistencia.registro ALTER COLUMN hora_entrada SET (pg_attver=169589768);
ALTER TABLE ONLY asistencia.registro ALTER COLUMN hora_salida SET (pg_attver=169589768);
ALTER TABLE ONLY asistencia.registro ALTER COLUMN cod_fuente_marcacion SET (pg_attver=169589768);
ALTER TABLE ONLY asistencia.registro ALTER COLUMN observacion SET (pg_attver=169589768);
ALTER TABLE ONLY asistencia.registro ALTER COLUMN registrado_por SET (pg_attver=169589768);
ALTER TABLE ONLY asistencia.registro ALTER COLUMN creado_en SET (pg_attver=169589768);
ALTER TABLE ONLY asistencia.registro ALTER COLUMN salida_no_registrada SET (pg_attver=1946157144);


--
-- Name: COLUMN registro.salida_no_registrada; Type: COMMENT; Schema: asistencia; Owner: -
--

COMMENT ON COLUMN asistencia.registro.salida_no_registrada IS 'La jornada quedó sin salida y se cerró automáticamente al vencer. El administrador la corrige con el registro manual.';


--
-- Name: registro_id_registro_seq; Type: SEQUENCE; Schema: asistencia; Owner: -
--

ALTER TABLE asistencia.registro ALTER COLUMN id_registro ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME asistencia.registro_id_registro_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: asignacion; Type: TABLE; Schema: turnos; Owner: -
--

CREATE TABLE turnos.asignacion (
    id_asignacion bigint NOT NULL,
    id_empleado bigint NOT NULL,
    id_turno integer NOT NULL,
    fecha date NOT NULL,
    periodo tstzrange NOT NULL,
    cod_estado_asignacion character varying(20) DEFAULT 'PROGRAMADA'::character varying NOT NULL,
    observacion character varying(300),
    creado_por bigint NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT asignacion_periodo_check CHECK (((NOT isempty(periodo)) AND (lower_inf(periodo) = false) AND (upper_inf(periodo) = false)))
);
ALTER TABLE ONLY turnos.asignacion ALTER COLUMN id_asignacion SET (pg_attver=169428632);
ALTER TABLE ONLY turnos.asignacion ALTER COLUMN id_empleado SET (pg_attver=169428632);
ALTER TABLE ONLY turnos.asignacion ALTER COLUMN id_turno SET (pg_attver=169428632);
ALTER TABLE ONLY turnos.asignacion ALTER COLUMN fecha SET (pg_attver=169428632);
ALTER TABLE ONLY turnos.asignacion ALTER COLUMN periodo SET (pg_attver=169428632);
ALTER TABLE ONLY turnos.asignacion ALTER COLUMN cod_estado_asignacion SET (pg_attver=169428632);
ALTER TABLE ONLY turnos.asignacion ALTER COLUMN observacion SET (pg_attver=169428632);
ALTER TABLE ONLY turnos.asignacion ALTER COLUMN creado_por SET (pg_attver=169428632);
ALTER TABLE ONLY turnos.asignacion ALTER COLUMN creado_en SET (pg_attver=169428632);


--
-- Name: turno; Type: TABLE; Schema: turnos; Owner: -
--

CREATE TABLE turnos.turno (
    id_turno integer NOT NULL,
    nombre character varying(60) NOT NULL,
    hora_inicio time without time zone NOT NULL,
    hora_fin time without time zone NOT NULL,
    minutos_descanso integer DEFAULT 0 NOT NULL,
    cruza_medianoche boolean GENERATED ALWAYS AS ((hora_fin <= hora_inicio)) STORED,
    duracion_minutos integer GENERATED ALWAYS AS (((
CASE
    WHEN (hora_fin > hora_inicio) THEN EXTRACT(epoch FROM (hora_fin - hora_inicio))
    ELSE (EXTRACT(epoch FROM (hora_fin - hora_inicio)) + (86400)::numeric)
END / (60)::numeric))::integer) STORED,
    color character varying(7),
    activo boolean DEFAULT true NOT NULL,
    CONSTRAINT turno_minutos_descanso_check CHECK ((minutos_descanso >= 0))
);
ALTER TABLE ONLY turnos.turno ALTER COLUMN id_turno SET (pg_attver=169400296);
ALTER TABLE ONLY turnos.turno ALTER COLUMN nombre SET (pg_attver=169400296);
ALTER TABLE ONLY turnos.turno ALTER COLUMN hora_inicio SET (pg_attver=169400296);
ALTER TABLE ONLY turnos.turno ALTER COLUMN hora_fin SET (pg_attver=169400296);
ALTER TABLE ONLY turnos.turno ALTER COLUMN minutos_descanso SET (pg_attver=169400296);
ALTER TABLE ONLY turnos.turno ALTER COLUMN cruza_medianoche SET (pg_attver=169400296);
ALTER TABLE ONLY turnos.turno ALTER COLUMN duracion_minutos SET (pg_attver=169400296);
ALTER TABLE ONLY turnos.turno ALTER COLUMN color SET (pg_attver=169400296);
ALTER TABLE ONLY turnos.turno ALTER COLUMN activo SET (pg_attver=169400296);


--
-- Name: v_horas_por_turno; Type: VIEW; Schema: asistencia; Owner: -
--

CREATE VIEW asistencia.v_horas_por_turno AS
 SELECT a.id_asignacion,
    a.id_empleado,
    a.fecha,
    a.id_turno,
    (((EXTRACT(epoch FROM (upper(a.periodo) - lower(a.periodo))) / (60)::numeric))::integer - t.minutos_descanso) AS minutos_programados,
    COALESCE((((EXTRACT(epoch FROM (r.hora_salida - r.hora_entrada)) / (60)::numeric))::integer - t.minutos_descanso), 0) AS minutos_trabajados,
    r.hora_entrada,
    r.hora_salida,
    GREATEST(((EXTRACT(epoch FROM (r.hora_entrada - lower(a.periodo))) / (60)::numeric))::integer, 0) AS minutos_tarde,
    ((r.id_registro IS NULL) AND (upper(a.periodo) < now())) AS sin_asistencia
   FROM ((turnos.asignacion a
     JOIN turnos.turno t USING (id_turno))
     LEFT JOIN asistencia.registro r ON ((r.id_asignacion = a.id_asignacion)))
  WHERE ((a.cod_estado_asignacion)::text <> 'ANULADA'::text);


--
-- Name: bitacora_negocio; Type: TABLE; Schema: auditoria; Owner: -
--

CREATE TABLE auditoria.bitacora_negocio (
    id_bitacora bigint NOT NULL,
    fecha_hora timestamp with time zone DEFAULT now() NOT NULL,
    id_usuario bigint,
    cod_tipo_accion character varying(20) NOT NULL,
    esquema character varying(63) NOT NULL,
    tabla character varying(63) NOT NULL,
    id_registro character varying(60) NOT NULL,
    id_empleado bigint,
    valor_anterior jsonb,
    valor_nuevo jsonb,
    descripcion character varying(300),
    ip_origen inet
);
ALTER TABLE ONLY auditoria.bitacora_negocio ALTER COLUMN id_bitacora SET (pg_attver=169836432);
ALTER TABLE ONLY auditoria.bitacora_negocio ALTER COLUMN fecha_hora SET (pg_attver=169836432);
ALTER TABLE ONLY auditoria.bitacora_negocio ALTER COLUMN id_usuario SET (pg_attver=169836432);
ALTER TABLE ONLY auditoria.bitacora_negocio ALTER COLUMN cod_tipo_accion SET (pg_attver=169836432);
ALTER TABLE ONLY auditoria.bitacora_negocio ALTER COLUMN esquema SET (pg_attver=169836432);
ALTER TABLE ONLY auditoria.bitacora_negocio ALTER COLUMN tabla SET (pg_attver=169836432);
ALTER TABLE ONLY auditoria.bitacora_negocio ALTER COLUMN id_registro SET (pg_attver=169836432);
ALTER TABLE ONLY auditoria.bitacora_negocio ALTER COLUMN id_empleado SET (pg_attver=169836432);
ALTER TABLE ONLY auditoria.bitacora_negocio ALTER COLUMN valor_anterior SET (pg_attver=169836432);
ALTER TABLE ONLY auditoria.bitacora_negocio ALTER COLUMN valor_nuevo SET (pg_attver=169836432);
ALTER TABLE ONLY auditoria.bitacora_negocio ALTER COLUMN descripcion SET (pg_attver=169836432);
ALTER TABLE ONLY auditoria.bitacora_negocio ALTER COLUMN ip_origen SET (pg_attver=169836432);


--
-- Name: bitacora_negocio_id_bitacora_seq; Type: SEQUENCE; Schema: auditoria; Owner: -
--

ALTER TABLE auditoria.bitacora_negocio ALTER COLUMN id_bitacora ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME auditoria.bitacora_negocio_id_bitacora_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: novedad; Type: TABLE; Schema: novedades; Owner: -
--

CREATE TABLE novedades.novedad (
    id_novedad bigint NOT NULL,
    id_empleado bigint NOT NULL,
    cod_tipo_novedad character varying(30) NOT NULL,
    inicio timestamp with time zone NOT NULL,
    fin timestamp with time zone NOT NULL,
    remunerado boolean NOT NULL,
    afecta_horas boolean NOT NULL,
    cod_estado_solicitud character varying(20) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    observaciones text,
    url_soporte character varying(500),
    registrado_por bigint NOT NULL,
    registrado_en timestamp with time zone DEFAULT now() NOT NULL,
    resuelto_por bigint,
    resuelto_en timestamp with time zone,
    motivo_rechazo character varying(300),
    CONSTRAINT novedad_check CHECK ((fin > inicio)),
    CONSTRAINT novedad_check1 CHECK (((resuelto_por IS NULL) = (resuelto_en IS NULL)))
);
ALTER TABLE ONLY novedades.novedad ALTER COLUMN id_novedad SET (pg_attver=169759984);
ALTER TABLE ONLY novedades.novedad ALTER COLUMN id_empleado SET (pg_attver=169759984);
ALTER TABLE ONLY novedades.novedad ALTER COLUMN cod_tipo_novedad SET (pg_attver=169759984);
ALTER TABLE ONLY novedades.novedad ALTER COLUMN inicio SET (pg_attver=169759984);
ALTER TABLE ONLY novedades.novedad ALTER COLUMN fin SET (pg_attver=169759984);
ALTER TABLE ONLY novedades.novedad ALTER COLUMN remunerado SET (pg_attver=169759984);
ALTER TABLE ONLY novedades.novedad ALTER COLUMN afecta_horas SET (pg_attver=169759984);
ALTER TABLE ONLY novedades.novedad ALTER COLUMN cod_estado_solicitud SET (pg_attver=169759984);
ALTER TABLE ONLY novedades.novedad ALTER COLUMN observaciones SET (pg_attver=169759984);
ALTER TABLE ONLY novedades.novedad ALTER COLUMN url_soporte SET (pg_attver=169759984);
ALTER TABLE ONLY novedades.novedad ALTER COLUMN registrado_por SET (pg_attver=169759984);
ALTER TABLE ONLY novedades.novedad ALTER COLUMN registrado_en SET (pg_attver=169759984);
ALTER TABLE ONLY novedades.novedad ALTER COLUMN resuelto_por SET (pg_attver=169759984);
ALTER TABLE ONLY novedades.novedad ALTER COLUMN resuelto_en SET (pg_attver=169759984);
ALTER TABLE ONLY novedades.novedad ALTER COLUMN motivo_rechazo SET (pg_attver=169759984);


--
-- Name: novedad_id_novedad_seq; Type: SEQUENCE; Schema: novedades; Owner: -
--

ALTER TABLE novedades.novedad ALTER COLUMN id_novedad ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME novedades.novedad_id_novedad_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: periodo_vacacional; Type: TABLE; Schema: novedades; Owner: -
--

CREATE TABLE novedades.periodo_vacacional (
    id_periodo_vacacional bigint NOT NULL,
    id_empleado bigint NOT NULL,
    fecha_inicio date NOT NULL,
    fecha_fin date NOT NULL,
    dias_otorgados numeric(5,2) NOT NULL,
    CONSTRAINT periodo_vacacional_check CHECK ((fecha_fin > fecha_inicio)),
    CONSTRAINT periodo_vacacional_dias_otorgados_check CHECK ((dias_otorgados >= (0)::numeric))
);
ALTER TABLE ONLY novedades.periodo_vacacional ALTER COLUMN id_periodo_vacacional SET (pg_attver=169664048);
ALTER TABLE ONLY novedades.periodo_vacacional ALTER COLUMN id_empleado SET (pg_attver=169664048);
ALTER TABLE ONLY novedades.periodo_vacacional ALTER COLUMN fecha_inicio SET (pg_attver=169664048);
ALTER TABLE ONLY novedades.periodo_vacacional ALTER COLUMN fecha_fin SET (pg_attver=169664048);
ALTER TABLE ONLY novedades.periodo_vacacional ALTER COLUMN dias_otorgados SET (pg_attver=169664048);


--
-- Name: periodo_vacacional_id_periodo_vacacional_seq; Type: SEQUENCE; Schema: novedades; Owner: -
--

ALTER TABLE novedades.periodo_vacacional ALTER COLUMN id_periodo_vacacional ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME novedades.periodo_vacacional_id_periodo_vacacional_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: solicitud_vacaciones; Type: TABLE; Schema: novedades; Owner: -
--

CREATE TABLE novedades.solicitud_vacaciones (
    id_solicitud_vacaciones bigint NOT NULL,
    id_empleado bigint NOT NULL,
    fecha_inicio date NOT NULL,
    fecha_fin date NOT NULL,
    dias_solicitados numeric(5,2) NOT NULL,
    cod_estado_solicitud character varying(20) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    observacion character varying(300),
    solicitado_por bigint NOT NULL,
    solicitado_en timestamp with time zone DEFAULT now() NOT NULL,
    resuelto_por bigint,
    resuelto_en timestamp with time zone,
    motivo_rechazo character varying(300),
    CONSTRAINT solicitud_vacaciones_check CHECK ((fecha_fin >= fecha_inicio)),
    CONSTRAINT solicitud_vacaciones_check1 CHECK (((resuelto_por IS NULL) = (resuelto_en IS NULL))),
    CONSTRAINT solicitud_vacaciones_dias_solicitados_check CHECK ((dias_solicitados > (0)::numeric))
);
ALTER TABLE ONLY novedades.solicitud_vacaciones ALTER COLUMN id_solicitud_vacaciones SET (pg_attver=169693848);
ALTER TABLE ONLY novedades.solicitud_vacaciones ALTER COLUMN id_empleado SET (pg_attver=169693848);
ALTER TABLE ONLY novedades.solicitud_vacaciones ALTER COLUMN fecha_inicio SET (pg_attver=169693848);
ALTER TABLE ONLY novedades.solicitud_vacaciones ALTER COLUMN fecha_fin SET (pg_attver=169693848);
ALTER TABLE ONLY novedades.solicitud_vacaciones ALTER COLUMN dias_solicitados SET (pg_attver=169693848);
ALTER TABLE ONLY novedades.solicitud_vacaciones ALTER COLUMN cod_estado_solicitud SET (pg_attver=169693848);
ALTER TABLE ONLY novedades.solicitud_vacaciones ALTER COLUMN observacion SET (pg_attver=169693848);
ALTER TABLE ONLY novedades.solicitud_vacaciones ALTER COLUMN solicitado_por SET (pg_attver=169693848);
ALTER TABLE ONLY novedades.solicitud_vacaciones ALTER COLUMN solicitado_en SET (pg_attver=169693848);
ALTER TABLE ONLY novedades.solicitud_vacaciones ALTER COLUMN resuelto_por SET (pg_attver=169693848);
ALTER TABLE ONLY novedades.solicitud_vacaciones ALTER COLUMN resuelto_en SET (pg_attver=169693848);
ALTER TABLE ONLY novedades.solicitud_vacaciones ALTER COLUMN motivo_rechazo SET (pg_attver=169693848);


--
-- Name: solicitud_vacaciones_id_solicitud_vacaciones_seq; Type: SEQUENCE; Schema: novedades; Owner: -
--

ALTER TABLE novedades.solicitud_vacaciones ALTER COLUMN id_solicitud_vacaciones ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME novedades.solicitud_vacaciones_id_solicitud_vacaciones_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: empleado; Type: TABLE; Schema: organizacion; Owner: -
--

CREATE TABLE organizacion.empleado (
    id_empleado bigint NOT NULL,
    id_usuario bigint,
    cod_tipo_documento character varying(10) NOT NULL,
    numero_documento character varying(30) NOT NULL,
    nombres character varying(80) NOT NULL,
    apellidos character varying(80) NOT NULL,
    correo_contacto character varying(150),
    telefono character varying(30),
    direccion character varying(200),
    id_area integer NOT NULL,
    id_cargo integer NOT NULL,
    cod_tipo_contrato character varying(20),
    fecha_ingreso date NOT NULL,
    fecha_retiro date,
    cod_estado_empleado character varying(20) DEFAULT 'ACTIVO'::character varying NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT empleado_check CHECK (((fecha_retiro IS NULL) OR (fecha_retiro >= fecha_ingreso)))
);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN id_empleado SET (pg_attver=169308312);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN id_usuario SET (pg_attver=169308312);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN cod_tipo_documento SET (pg_attver=169308312);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN numero_documento SET (pg_attver=169308312);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN nombres SET (pg_attver=169308312);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN apellidos SET (pg_attver=169308312);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN correo_contacto SET (pg_attver=169308312);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN telefono SET (pg_attver=169308312);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN direccion SET (pg_attver=169308312);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN id_area SET (pg_attver=169308312);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN id_cargo SET (pg_attver=169308312);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN cod_tipo_contrato SET (pg_attver=169308312);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN fecha_ingreso SET (pg_attver=169308312);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN fecha_retiro SET (pg_attver=169308312);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN cod_estado_empleado SET (pg_attver=169308312);
ALTER TABLE ONLY organizacion.empleado ALTER COLUMN creado_en SET (pg_attver=169308312);


--
-- Name: v_saldo_vacaciones; Type: VIEW; Schema: novedades; Owner: -
--

CREATE VIEW novedades.v_saldo_vacaciones AS
 SELECT e.id_empleado,
    COALESCE(o.dias_otorgados, (0)::numeric) AS dias_otorgados,
    COALESCE(s.dias_aprobados, (0)::numeric) AS dias_utilizados,
    COALESCE(s.dias_pendientes, (0)::numeric) AS dias_en_solicitud,
    (COALESCE(o.dias_otorgados, (0)::numeric) - COALESCE(s.dias_aprobados, (0)::numeric)) AS dias_disponibles
   FROM ((organizacion.empleado e
     LEFT JOIN ( SELECT periodo_vacacional.id_empleado,
            sum(periodo_vacacional.dias_otorgados) AS dias_otorgados
           FROM novedades.periodo_vacacional
          GROUP BY periodo_vacacional.id_empleado) o USING (id_empleado))
     LEFT JOIN ( SELECT solicitud_vacaciones.id_empleado,
            sum(solicitud_vacaciones.dias_solicitados) FILTER (WHERE ((solicitud_vacaciones.cod_estado_solicitud)::text = 'APROBADA'::text)) AS dias_aprobados,
            sum(solicitud_vacaciones.dias_solicitados) FILTER (WHERE ((solicitud_vacaciones.cod_estado_solicitud)::text = 'PENDIENTE'::text)) AS dias_pendientes
           FROM novedades.solicitud_vacaciones
          GROUP BY solicitud_vacaciones.id_empleado) s USING (id_empleado));


--
-- Name: area; Type: TABLE; Schema: organizacion; Owner: -
--

CREATE TABLE organizacion.area (
    id_area integer NOT NULL,
    id_sucursal integer NOT NULL,
    nombre character varying(100) NOT NULL,
    activo boolean DEFAULT true NOT NULL
);
ALTER TABLE ONLY organizacion.area ALTER COLUMN id_area SET (pg_attver=169262880);
ALTER TABLE ONLY organizacion.area ALTER COLUMN id_sucursal SET (pg_attver=169262880);
ALTER TABLE ONLY organizacion.area ALTER COLUMN nombre SET (pg_attver=169262880);
ALTER TABLE ONLY organizacion.area ALTER COLUMN activo SET (pg_attver=169262880);


--
-- Name: area_id_area_seq; Type: SEQUENCE; Schema: organizacion; Owner: -
--

ALTER TABLE organizacion.area ALTER COLUMN id_area ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME organizacion.area_id_area_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: cargo; Type: TABLE; Schema: organizacion; Owner: -
--

CREATE TABLE organizacion.cargo (
    id_cargo integer NOT NULL,
    nombre character varying(100) NOT NULL,
    activo boolean DEFAULT true NOT NULL
);
ALTER TABLE ONLY organizacion.cargo ALTER COLUMN id_cargo SET (pg_attver=169288800);
ALTER TABLE ONLY organizacion.cargo ALTER COLUMN nombre SET (pg_attver=169288800);
ALTER TABLE ONLY organizacion.cargo ALTER COLUMN activo SET (pg_attver=169288800);


--
-- Name: cargo_id_cargo_seq; Type: SEQUENCE; Schema: organizacion; Owner: -
--

ALTER TABLE organizacion.cargo ALTER COLUMN id_cargo ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME organizacion.cargo_id_cargo_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: empleado_id_empleado_seq; Type: SEQUENCE; Schema: organizacion; Owner: -
--

ALTER TABLE organizacion.empleado ALTER COLUMN id_empleado ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME organizacion.empleado_id_empleado_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: sucursal; Type: TABLE; Schema: organizacion; Owner: -
--

CREATE TABLE organizacion.sucursal (
    id_sucursal integer NOT NULL,
    nombre character varying(100) NOT NULL,
    direccion character varying(200),
    ciudad character varying(80),
    activo boolean DEFAULT true NOT NULL
);
ALTER TABLE ONLY organizacion.sucursal ALTER COLUMN id_sucursal SET (pg_attver=169242480);
ALTER TABLE ONLY organizacion.sucursal ALTER COLUMN nombre SET (pg_attver=169242480);
ALTER TABLE ONLY organizacion.sucursal ALTER COLUMN direccion SET (pg_attver=169242480);
ALTER TABLE ONLY organizacion.sucursal ALTER COLUMN ciudad SET (pg_attver=169242480);
ALTER TABLE ONLY organizacion.sucursal ALTER COLUMN activo SET (pg_attver=169242480);


--
-- Name: sucursal_id_sucursal_seq; Type: SEQUENCE; Schema: organizacion; Owner: -
--

ALTER TABLE organizacion.sucursal ALTER COLUMN id_sucursal ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME organizacion.sucursal_id_sucursal_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: categoria_hora; Type: TABLE; Schema: parametrizacion; Owner: -
--

CREATE TABLE parametrizacion.categoria_hora (
    cod_categoria_hora character varying(30) NOT NULL,
    nombre character varying(80) NOT NULL,
    factor_pago numeric(5,2) DEFAULT 1.00 NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    CONSTRAINT categoria_hora_factor_pago_check CHECK ((factor_pago >= (0)::numeric))
);
ALTER TABLE ONLY parametrizacion.categoria_hora ALTER COLUMN cod_categoria_hora SET (pg_attver=168965216);
ALTER TABLE ONLY parametrizacion.categoria_hora ALTER COLUMN nombre SET (pg_attver=168965216);
ALTER TABLE ONLY parametrizacion.categoria_hora ALTER COLUMN factor_pago SET (pg_attver=168965216);
ALTER TABLE ONLY parametrizacion.categoria_hora ALTER COLUMN activo SET (pg_attver=168965216);


--
-- Name: dia_festivo; Type: TABLE; Schema: parametrizacion; Owner: -
--

CREATE TABLE parametrizacion.dia_festivo (
    fecha date NOT NULL,
    nombre character varying(100) NOT NULL
);
ALTER TABLE ONLY parametrizacion.dia_festivo ALTER COLUMN fecha SET (pg_attver=169007496);
ALTER TABLE ONLY parametrizacion.dia_festivo ALTER COLUMN nombre SET (pg_attver=169007496);


--
-- Name: estado_asignacion; Type: TABLE; Schema: parametrizacion; Owner: -
--

CREATE TABLE parametrizacion.estado_asignacion (
    cod_estado_asignacion character varying(20) NOT NULL,
    nombre character varying(60) NOT NULL
);
ALTER TABLE ONLY parametrizacion.estado_asignacion ALTER COLUMN cod_estado_asignacion SET (pg_attver=168933464);
ALTER TABLE ONLY parametrizacion.estado_asignacion ALTER COLUMN nombre SET (pg_attver=168933464);


--
-- Name: estado_empleado; Type: TABLE; Schema: parametrizacion; Owner: -
--

CREATE TABLE parametrizacion.estado_empleado (
    cod_estado_empleado character varying(20) NOT NULL,
    nombre character varying(60) NOT NULL
);
ALTER TABLE ONLY parametrizacion.estado_empleado ALTER COLUMN cod_estado_empleado SET (pg_attver=168904600);
ALTER TABLE ONLY parametrizacion.estado_empleado ALTER COLUMN nombre SET (pg_attver=168904600);


--
-- Name: estado_solicitud; Type: TABLE; Schema: parametrizacion; Owner: -
--

CREATE TABLE parametrizacion.estado_solicitud (
    cod_estado_solicitud character varying(20) NOT NULL,
    nombre character varying(60) NOT NULL,
    es_final boolean DEFAULT false NOT NULL
);
ALTER TABLE ONLY parametrizacion.estado_solicitud ALTER COLUMN cod_estado_solicitud SET (pg_attver=168917216);
ALTER TABLE ONLY parametrizacion.estado_solicitud ALTER COLUMN nombre SET (pg_attver=168917216);
ALTER TABLE ONLY parametrizacion.estado_solicitud ALTER COLUMN es_final SET (pg_attver=168917216);


--
-- Name: estado_usuario; Type: TABLE; Schema: parametrizacion; Owner: -
--

CREATE TABLE parametrizacion.estado_usuario (
    cod_estado_usuario character varying(20) NOT NULL,
    nombre character varying(60) NOT NULL
);
ALTER TABLE ONLY parametrizacion.estado_usuario ALTER COLUMN cod_estado_usuario SET (pg_attver=168886440);
ALTER TABLE ONLY parametrizacion.estado_usuario ALTER COLUMN nombre SET (pg_attver=168886440);


--
-- Name: fuente_marcacion; Type: TABLE; Schema: parametrizacion; Owner: -
--

CREATE TABLE parametrizacion.fuente_marcacion (
    cod_fuente_marcacion character varying(20) NOT NULL,
    nombre character varying(60) NOT NULL
);
ALTER TABLE ONLY parametrizacion.fuente_marcacion ALTER COLUMN cod_fuente_marcacion SET (pg_attver=168982760);
ALTER TABLE ONLY parametrizacion.fuente_marcacion ALTER COLUMN nombre SET (pg_attver=168982760);


--
-- Name: parametro; Type: TABLE; Schema: parametrizacion; Owner: -
--

CREATE TABLE parametrizacion.parametro (
    cod_parametro character varying(60) NOT NULL,
    nombre character varying(120) NOT NULL,
    descripcion text,
    tipo_dato character varying(10) NOT NULL,
    editable boolean DEFAULT true NOT NULL,
    CONSTRAINT parametro_tipo_dato_check CHECK (((tipo_dato)::text = ANY ((ARRAY['ENTERO'::character varying, 'DECIMAL'::character varying, 'TEXTO'::character varying, 'BOOLEANO'::character varying, 'HORA'::character varying])::text[])))
);
ALTER TABLE ONLY parametrizacion.parametro ALTER COLUMN cod_parametro SET (pg_attver=169016648);
ALTER TABLE ONLY parametrizacion.parametro ALTER COLUMN nombre SET (pg_attver=169016648);
ALTER TABLE ONLY parametrizacion.parametro ALTER COLUMN descripcion SET (pg_attver=169016648);
ALTER TABLE ONLY parametrizacion.parametro ALTER COLUMN tipo_dato SET (pg_attver=169016648);
ALTER TABLE ONLY parametrizacion.parametro ALTER COLUMN editable SET (pg_attver=169016648);


--
-- Name: parametro_valor; Type: TABLE; Schema: parametrizacion; Owner: -
--

CREATE TABLE parametrizacion.parametro_valor (
    id_parametro_valor bigint NOT NULL,
    cod_parametro character varying(60) NOT NULL,
    valor character varying(200) NOT NULL,
    vigente_desde date NOT NULL,
    vigente_hasta date,
    CONSTRAINT parametro_valor_check CHECK (((vigente_hasta IS NULL) OR (vigente_hasta >= vigente_desde)))
);
ALTER TABLE ONLY parametrizacion.parametro_valor ALTER COLUMN id_parametro_valor SET (pg_attver=169046568);
ALTER TABLE ONLY parametrizacion.parametro_valor ALTER COLUMN cod_parametro SET (pg_attver=169046568);
ALTER TABLE ONLY parametrizacion.parametro_valor ALTER COLUMN valor SET (pg_attver=169046568);
ALTER TABLE ONLY parametrizacion.parametro_valor ALTER COLUMN vigente_desde SET (pg_attver=169046568);
ALTER TABLE ONLY parametrizacion.parametro_valor ALTER COLUMN vigente_hasta SET (pg_attver=169046568);


--
-- Name: parametro_valor_id_parametro_valor_seq; Type: SEQUENCE; Schema: parametrizacion; Owner: -
--

ALTER TABLE parametrizacion.parametro_valor ALTER COLUMN id_parametro_valor ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME parametrizacion.parametro_valor_id_parametro_valor_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tipo_accion; Type: TABLE; Schema: parametrizacion; Owner: -
--

CREATE TABLE parametrizacion.tipo_accion (
    cod_tipo_accion character varying(20) NOT NULL,
    nombre character varying(60) NOT NULL
);
ALTER TABLE ONLY parametrizacion.tipo_accion ALTER COLUMN cod_tipo_accion SET (pg_attver=168995184);
ALTER TABLE ONLY parametrizacion.tipo_accion ALTER COLUMN nombre SET (pg_attver=168995184);


--
-- Name: tipo_contrato; Type: TABLE; Schema: parametrizacion; Owner: -
--

CREATE TABLE parametrizacion.tipo_contrato (
    cod_tipo_contrato character varying(20) NOT NULL,
    nombre character varying(60) NOT NULL,
    activo boolean DEFAULT true NOT NULL
);
ALTER TABLE ONLY parametrizacion.tipo_contrato ALTER COLUMN cod_tipo_contrato SET (pg_attver=168869944);
ALTER TABLE ONLY parametrizacion.tipo_contrato ALTER COLUMN nombre SET (pg_attver=168869944);
ALTER TABLE ONLY parametrizacion.tipo_contrato ALTER COLUMN activo SET (pg_attver=168869944);


--
-- Name: tipo_documento; Type: TABLE; Schema: parametrizacion; Owner: -
--

CREATE TABLE parametrizacion.tipo_documento (
    cod_tipo_documento character varying(10) NOT NULL,
    nombre character varying(60) NOT NULL,
    activo boolean DEFAULT true NOT NULL
);
ALTER TABLE ONLY parametrizacion.tipo_documento ALTER COLUMN cod_tipo_documento SET (pg_attver=168837320);
ALTER TABLE ONLY parametrizacion.tipo_documento ALTER COLUMN nombre SET (pg_attver=168837320);
ALTER TABLE ONLY parametrizacion.tipo_documento ALTER COLUMN activo SET (pg_attver=168837320);


--
-- Name: tipo_novedad; Type: TABLE; Schema: parametrizacion; Owner: -
--

CREATE TABLE parametrizacion.tipo_novedad (
    cod_tipo_novedad character varying(30) NOT NULL,
    nombre character varying(80) NOT NULL,
    remunerado_defecto boolean NOT NULL,
    afecta_horas_defecto boolean NOT NULL,
    requiere_aprobacion boolean DEFAULT true NOT NULL,
    requiere_soporte boolean DEFAULT false NOT NULL,
    activo boolean DEFAULT true NOT NULL
);
ALTER TABLE ONLY parametrizacion.tipo_novedad ALTER COLUMN cod_tipo_novedad SET (pg_attver=168946048);
ALTER TABLE ONLY parametrizacion.tipo_novedad ALTER COLUMN nombre SET (pg_attver=168946048);
ALTER TABLE ONLY parametrizacion.tipo_novedad ALTER COLUMN remunerado_defecto SET (pg_attver=168946048);
ALTER TABLE ONLY parametrizacion.tipo_novedad ALTER COLUMN afecta_horas_defecto SET (pg_attver=168946048);
ALTER TABLE ONLY parametrizacion.tipo_novedad ALTER COLUMN requiere_aprobacion SET (pg_attver=168946048);
ALTER TABLE ONLY parametrizacion.tipo_novedad ALTER COLUMN requiere_soporte SET (pg_attver=168946048);
ALTER TABLE ONLY parametrizacion.tipo_novedad ALTER COLUMN activo SET (pg_attver=168946048);


--
-- Name: DataProtectionKeys; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."DataProtectionKeys" (
    "Id" integer NOT NULL,
    "FriendlyName" text,
    "Xml" text
);
ALTER TABLE ONLY public."DataProtectionKeys" ALTER COLUMN "Id" SET (pg_attver=235000552);
ALTER TABLE ONLY public."DataProtectionKeys" ALTER COLUMN "FriendlyName" SET (pg_attver=235000552);
ALTER TABLE ONLY public."DataProtectionKeys" ALTER COLUMN "Xml" SET (pg_attver=235000552);


--
-- Name: DataProtectionKeys_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."DataProtectionKeys" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."DataProtectionKeys_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: permiso; Type: TABLE; Schema: seguridad; Owner: -
--

CREATE TABLE seguridad.permiso (
    id_permiso integer NOT NULL,
    codigo character varying(80) NOT NULL,
    descripcion character varying(200) NOT NULL
);
ALTER TABLE ONLY seguridad.permiso ALTER COLUMN id_permiso SET (pg_attver=169105872);
ALTER TABLE ONLY seguridad.permiso ALTER COLUMN codigo SET (pg_attver=169105872);
ALTER TABLE ONLY seguridad.permiso ALTER COLUMN descripcion SET (pg_attver=169105872);


--
-- Name: permiso_id_permiso_seq; Type: SEQUENCE; Schema: seguridad; Owner: -
--

ALTER TABLE seguridad.permiso ALTER COLUMN id_permiso ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME seguridad.permiso_id_permiso_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: rol; Type: TABLE; Schema: seguridad; Owner: -
--

CREATE TABLE seguridad.rol (
    id_rol integer NOT NULL,
    nombre character varying(50) NOT NULL,
    descripcion character varying(200),
    activo boolean DEFAULT true NOT NULL
);
ALTER TABLE ONLY seguridad.rol ALTER COLUMN id_rol SET (pg_attver=169084672);
ALTER TABLE ONLY seguridad.rol ALTER COLUMN nombre SET (pg_attver=169084672);
ALTER TABLE ONLY seguridad.rol ALTER COLUMN descripcion SET (pg_attver=169084672);
ALTER TABLE ONLY seguridad.rol ALTER COLUMN activo SET (pg_attver=169084672);


--
-- Name: rol_id_rol_seq; Type: SEQUENCE; Schema: seguridad; Owner: -
--

ALTER TABLE seguridad.rol ALTER COLUMN id_rol ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME seguridad.rol_id_rol_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: rol_permiso; Type: TABLE; Schema: seguridad; Owner: -
--

CREATE TABLE seguridad.rol_permiso (
    id_rol integer NOT NULL,
    id_permiso integer NOT NULL
);
ALTER TABLE ONLY seguridad.rol_permiso ALTER COLUMN id_rol SET (pg_attver=169119176);
ALTER TABLE ONLY seguridad.rol_permiso ALTER COLUMN id_permiso SET (pg_attver=169119176);


--
-- Name: sesion_dispositivo; Type: TABLE; Schema: seguridad; Owner: -
--

CREATE TABLE seguridad.sesion_dispositivo (
    id_sesion bigint NOT NULL,
    id_usuario bigint NOT NULL,
    hash_token character varying(64) NOT NULL,
    familia uuid NOT NULL,
    recordar boolean DEFAULT false NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    expira_en timestamp with time zone NOT NULL,
    revocada_en timestamp with time zone,
    motivo_revocacion character varying(30),
    user_agent character varying(300),
    ip_origen character varying(64)
);
ALTER TABLE ONLY seguridad.sesion_dispositivo ALTER COLUMN id_sesion SET (pg_attver=1946218448);
ALTER TABLE ONLY seguridad.sesion_dispositivo ALTER COLUMN id_usuario SET (pg_attver=1946218448);
ALTER TABLE ONLY seguridad.sesion_dispositivo ALTER COLUMN hash_token SET (pg_attver=1946218448);
ALTER TABLE ONLY seguridad.sesion_dispositivo ALTER COLUMN familia SET (pg_attver=1946218448);
ALTER TABLE ONLY seguridad.sesion_dispositivo ALTER COLUMN recordar SET (pg_attver=1946218448);
ALTER TABLE ONLY seguridad.sesion_dispositivo ALTER COLUMN creado_en SET (pg_attver=1946218448);
ALTER TABLE ONLY seguridad.sesion_dispositivo ALTER COLUMN expira_en SET (pg_attver=1946218448);
ALTER TABLE ONLY seguridad.sesion_dispositivo ALTER COLUMN revocada_en SET (pg_attver=1946218448);
ALTER TABLE ONLY seguridad.sesion_dispositivo ALTER COLUMN motivo_revocacion SET (pg_attver=1946218448);
ALTER TABLE ONLY seguridad.sesion_dispositivo ALTER COLUMN user_agent SET (pg_attver=1946218448);
ALTER TABLE ONLY seguridad.sesion_dispositivo ALTER COLUMN ip_origen SET (pg_attver=1946218448);


--
-- Name: TABLE sesion_dispositivo; Type: COMMENT; Schema: seguridad; Owner: -
--

COMMENT ON TABLE seguridad.sesion_dispositivo IS 'Refresh tokens por dispositivo (solo el hash SHA-256). Se rotan en cada uso; reutilizar uno rotado revoca la familia.';


--
-- Name: sesion_dispositivo_id_sesion_seq; Type: SEQUENCE; Schema: seguridad; Owner: -
--

ALTER TABLE seguridad.sesion_dispositivo ALTER COLUMN id_sesion ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME seguridad.sesion_dispositivo_id_sesion_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: solicitud_registro; Type: TABLE; Schema: seguridad; Owner: -
--

CREATE TABLE seguridad.solicitud_registro (
    id_solicitud_registro bigint NOT NULL,
    cod_tipo_documento character varying(10) NOT NULL,
    numero_documento character varying(30) NOT NULL,
    nombres character varying(80) NOT NULL,
    apellidos character varying(80) NOT NULL,
    correo character varying(150) NOT NULL,
    telefono character varying(30),
    hash_contrasena character varying(100) NOT NULL,
    cod_estado_solicitud character varying(20) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    solicitado_en timestamp with time zone DEFAULT now() NOT NULL,
    revisado_por bigint,
    revisado_en timestamp with time zone,
    motivo_rechazo character varying(300),
    id_usuario_creado bigint,
    CONSTRAINT solicitud_registro_check CHECK (((revisado_por IS NULL) = (revisado_en IS NULL)))
);
ALTER TABLE ONLY seguridad.solicitud_registro ALTER COLUMN id_solicitud_registro SET (pg_attver=169178408);
ALTER TABLE ONLY seguridad.solicitud_registro ALTER COLUMN cod_tipo_documento SET (pg_attver=169178408);
ALTER TABLE ONLY seguridad.solicitud_registro ALTER COLUMN numero_documento SET (pg_attver=169178408);
ALTER TABLE ONLY seguridad.solicitud_registro ALTER COLUMN nombres SET (pg_attver=169178408);
ALTER TABLE ONLY seguridad.solicitud_registro ALTER COLUMN apellidos SET (pg_attver=169178408);
ALTER TABLE ONLY seguridad.solicitud_registro ALTER COLUMN correo SET (pg_attver=169178408);
ALTER TABLE ONLY seguridad.solicitud_registro ALTER COLUMN telefono SET (pg_attver=169178408);
ALTER TABLE ONLY seguridad.solicitud_registro ALTER COLUMN hash_contrasena SET (pg_attver=169178408);
ALTER TABLE ONLY seguridad.solicitud_registro ALTER COLUMN cod_estado_solicitud SET (pg_attver=169178408);
ALTER TABLE ONLY seguridad.solicitud_registro ALTER COLUMN solicitado_en SET (pg_attver=169178408);
ALTER TABLE ONLY seguridad.solicitud_registro ALTER COLUMN revisado_por SET (pg_attver=169178408);
ALTER TABLE ONLY seguridad.solicitud_registro ALTER COLUMN revisado_en SET (pg_attver=169178408);
ALTER TABLE ONLY seguridad.solicitud_registro ALTER COLUMN motivo_rechazo SET (pg_attver=169178408);
ALTER TABLE ONLY seguridad.solicitud_registro ALTER COLUMN id_usuario_creado SET (pg_attver=169178408);


--
-- Name: solicitud_registro_id_solicitud_registro_seq; Type: SEQUENCE; Schema: seguridad; Owner: -
--

ALTER TABLE seguridad.solicitud_registro ALTER COLUMN id_solicitud_registro ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME seguridad.solicitud_registro_id_solicitud_registro_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: usuario; Type: TABLE; Schema: seguridad; Owner: -
--

CREATE TABLE seguridad.usuario (
    id_usuario bigint NOT NULL,
    correo character varying(150) NOT NULL,
    hash_contrasena character varying(100) NOT NULL,
    id_rol integer NOT NULL,
    cod_estado_usuario character varying(20) DEFAULT 'ACTIVO'::character varying NOT NULL,
    ultimo_acceso timestamp with time zone,
    creado_en timestamp with time zone DEFAULT now() NOT NULL
);
ALTER TABLE ONLY seguridad.usuario ALTER COLUMN id_usuario SET (pg_attver=169144680);
ALTER TABLE ONLY seguridad.usuario ALTER COLUMN correo SET (pg_attver=169144680);
ALTER TABLE ONLY seguridad.usuario ALTER COLUMN hash_contrasena SET (pg_attver=169144680);
ALTER TABLE ONLY seguridad.usuario ALTER COLUMN id_rol SET (pg_attver=169144680);
ALTER TABLE ONLY seguridad.usuario ALTER COLUMN cod_estado_usuario SET (pg_attver=169144680);
ALTER TABLE ONLY seguridad.usuario ALTER COLUMN ultimo_acceso SET (pg_attver=169144680);
ALTER TABLE ONLY seguridad.usuario ALTER COLUMN creado_en SET (pg_attver=169144680);


--
-- Name: usuario_foto_perfil; Type: TABLE; Schema: seguridad; Owner: -
--

CREATE TABLE seguridad.usuario_foto_perfil (
    id_usuario bigint NOT NULL,
    url character varying(500) NOT NULL,
    public_id character varying(255) NOT NULL,
    actualizado_en timestamp with time zone DEFAULT now() NOT NULL
);
ALTER TABLE ONLY seguridad.usuario_foto_perfil ALTER COLUMN id_usuario SET (pg_attver=1711295088);
ALTER TABLE ONLY seguridad.usuario_foto_perfil ALTER COLUMN url SET (pg_attver=1711295088);
ALTER TABLE ONLY seguridad.usuario_foto_perfil ALTER COLUMN public_id SET (pg_attver=1711295088);
ALTER TABLE ONLY seguridad.usuario_foto_perfil ALTER COLUMN actualizado_en SET (pg_attver=1711295088);


--
-- Name: TABLE usuario_foto_perfil; Type: COMMENT; Schema: seguridad; Owner: -
--

COMMENT ON TABLE seguridad.usuario_foto_perfil IS 'Foto de perfil del usuario. La imagen vive en Cloudinary; aquí solo se guarda su referencia.';


--
-- Name: COLUMN usuario_foto_perfil.url; Type: COMMENT; Schema: seguridad; Owner: -
--

COMMENT ON COLUMN seguridad.usuario_foto_perfil.url IS 'URL segura (https) de la imagen, con versión para invalidar cachés.';


--
-- Name: COLUMN usuario_foto_perfil.public_id; Type: COMMENT; Schema: seguridad; Owner: -
--

COMMENT ON COLUMN seguridad.usuario_foto_perfil.public_id IS 'Identificador del recurso en Cloudinary (para reemplazarlo o eliminarlo).';


--
-- Name: usuario_id_usuario_seq; Type: SEQUENCE; Schema: seguridad; Owner: -
--

ALTER TABLE seguridad.usuario ALTER COLUMN id_usuario ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME seguridad.usuario_id_usuario_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: asignacion_id_asignacion_seq; Type: SEQUENCE; Schema: turnos; Owner: -
--

ALTER TABLE turnos.asignacion ALTER COLUMN id_asignacion ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME turnos.asignacion_id_asignacion_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: dia_descanso; Type: TABLE; Schema: turnos; Owner: -
--

CREATE TABLE turnos.dia_descanso (
    id_dia_descanso bigint NOT NULL,
    id_empleado bigint NOT NULL,
    fecha date NOT NULL,
    creado_por bigint NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL
);
ALTER TABLE ONLY turnos.dia_descanso ALTER COLUMN id_dia_descanso SET (pg_attver=169505800);
ALTER TABLE ONLY turnos.dia_descanso ALTER COLUMN id_empleado SET (pg_attver=169505800);
ALTER TABLE ONLY turnos.dia_descanso ALTER COLUMN fecha SET (pg_attver=169505800);
ALTER TABLE ONLY turnos.dia_descanso ALTER COLUMN creado_por SET (pg_attver=169505800);
ALTER TABLE ONLY turnos.dia_descanso ALTER COLUMN creado_en SET (pg_attver=169505800);


--
-- Name: dia_descanso_id_dia_descanso_seq; Type: SEQUENCE; Schema: turnos; Owner: -
--

ALTER TABLE turnos.dia_descanso ALTER COLUMN id_dia_descanso ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME turnos.dia_descanso_id_dia_descanso_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: solicitud_cambio_turno; Type: TABLE; Schema: turnos; Owner: -
--

CREATE TABLE turnos.solicitud_cambio_turno (
    id_solicitud_cambio bigint NOT NULL,
    id_asignacion_solicitante bigint NOT NULL,
    id_asignacion_companero bigint NOT NULL,
    motivo character varying(300),
    cod_estado_solicitud character varying(20) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    solicitado_en timestamp with time zone DEFAULT now() NOT NULL,
    resuelto_por bigint,
    resuelto_en timestamp with time zone,
    motivo_rechazo character varying(300),
    CONSTRAINT solicitud_cambio_turno_check CHECK ((id_asignacion_solicitante <> id_asignacion_companero)),
    CONSTRAINT solicitud_cambio_turno_check1 CHECK (((resuelto_por IS NULL) = (resuelto_en IS NULL)))
);
ALTER TABLE ONLY turnos.solicitud_cambio_turno ALTER COLUMN id_solicitud_cambio SET (pg_attver=169536840);
ALTER TABLE ONLY turnos.solicitud_cambio_turno ALTER COLUMN id_asignacion_solicitante SET (pg_attver=169536840);
ALTER TABLE ONLY turnos.solicitud_cambio_turno ALTER COLUMN id_asignacion_companero SET (pg_attver=169536840);
ALTER TABLE ONLY turnos.solicitud_cambio_turno ALTER COLUMN motivo SET (pg_attver=169536840);
ALTER TABLE ONLY turnos.solicitud_cambio_turno ALTER COLUMN cod_estado_solicitud SET (pg_attver=169536840);
ALTER TABLE ONLY turnos.solicitud_cambio_turno ALTER COLUMN solicitado_en SET (pg_attver=169536840);
ALTER TABLE ONLY turnos.solicitud_cambio_turno ALTER COLUMN resuelto_por SET (pg_attver=169536840);
ALTER TABLE ONLY turnos.solicitud_cambio_turno ALTER COLUMN resuelto_en SET (pg_attver=169536840);
ALTER TABLE ONLY turnos.solicitud_cambio_turno ALTER COLUMN motivo_rechazo SET (pg_attver=169536840);


--
-- Name: solicitud_cambio_turno_id_solicitud_cambio_seq; Type: SEQUENCE; Schema: turnos; Owner: -
--

ALTER TABLE turnos.solicitud_cambio_turno ALTER COLUMN id_solicitud_cambio ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME turnos.solicitud_cambio_turno_id_solicitud_cambio_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: turno_id_turno_seq; Type: SEQUENCE; Schema: turnos; Owner: -
--

ALTER TABLE turnos.turno ALTER COLUMN id_turno ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME turnos.turno_id_turno_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: v_calendario; Type: VIEW; Schema: turnos; Owner: -
--

CREATE VIEW turnos.v_calendario AS
 SELECT a.id_empleado,
    'TURNO'::text AS tipo,
    t.nombre AS detalle,
    lower(a.periodo) AS inicio,
    upper(a.periodo) AS fin
   FROM (turnos.asignacion a
     JOIN turnos.turno t USING (id_turno))
  WHERE ((a.cod_estado_asignacion)::text <> 'ANULADA'::text)
UNION ALL
 SELECT d.id_empleado,
    'DESCANSO'::text AS tipo,
    'Día de descanso'::character varying AS detalle,
    (d.fecha)::timestamp with time zone AS inicio,
    ((d.fecha + 1))::timestamp with time zone AS fin
   FROM turnos.dia_descanso d
UNION ALL
 SELECT v.id_empleado,
    'VACACIONES'::text AS tipo,
    'Vacaciones'::character varying AS detalle,
    (v.fecha_inicio)::timestamp with time zone AS inicio,
    ((v.fecha_fin + 1))::timestamp with time zone AS fin
   FROM novedades.solicitud_vacaciones v
  WHERE ((v.cod_estado_solicitud)::text = 'APROBADA'::text)
UNION ALL
 SELECT n.id_empleado,
    'NOVEDAD'::text AS tipo,
    tn.nombre AS detalle,
    n.inicio,
    n.fin
   FROM (novedades.novedad n
     JOIN parametrizacion.tipo_novedad tn USING (cod_tipo_novedad))
  WHERE ((n.cod_estado_solicitud)::text = 'APROBADA'::text);


--
-- Data for Name: detalle_horas; Type: TABLE DATA; Schema: asistencia; Owner: -
--

INSERT INTO asistencia.detalle_horas VALUES
	(1, 'ORDINARIA', 627);


--
-- Data for Name: registro; Type: TABLE DATA; Schema: asistencia; Owner: -
--

INSERT INTO asistencia.registro OVERRIDING SYSTEM VALUE VALUES
	(1, 6, 3, '2026-11-18 06:18:00-05', '2026-11-18 17:14:34.202654-05', 'ADMIN', 'Tenia una reunión a las 6:20 a.m.  y no realizó el marcaje recién llegó', 6, '2026-11-18 07:44:01.102323-05', false);


--
-- Data for Name: bitacora_negocio; Type: TABLE DATA; Schema: auditoria; Owner: -
--

INSERT INTO auditoria.bitacora_negocio OVERRIDING SYSTEM VALUE VALUES
	(1, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'parametrizacion', 'parametro_valor', '1', NULL, NULL, '{"valor": "10", "cod_parametro": "JORNADA_MAX_HORAS_DIA", "vigente_desde": "2026-01-01", "vigente_hasta": null, "id_parametro_valor": 1}', NULL, NULL),
	(2, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'parametrizacion', 'parametro_valor', '2', NULL, NULL, '{"valor": "44", "cod_parametro": "JORNADA_MAX_HORAS_SEMANA", "vigente_desde": "2026-01-01", "vigente_hasta": null, "id_parametro_valor": 2}', NULL, NULL),
	(3, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'parametrizacion', 'parametro_valor', '3', NULL, NULL, '{"valor": "12", "cod_parametro": "DESCANSO_MIN_ENTRE_TURNOS", "vigente_desde": "2026-01-01", "vigente_hasta": null, "id_parametro_valor": 3}', NULL, NULL),
	(4, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'parametrizacion', 'parametro_valor', '4', NULL, NULL, '{"valor": "19:00", "cod_parametro": "HORA_INICIO_NOCTURNA", "vigente_desde": "2026-01-01", "vigente_hasta": null, "id_parametro_valor": 4}', NULL, NULL),
	(5, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'parametrizacion', 'parametro_valor', '5', NULL, NULL, '{"valor": "06:00", "cod_parametro": "HORA_FIN_NOCTURNA", "vigente_desde": "2026-01-01", "vigente_hasta": null, "id_parametro_valor": 5}', NULL, NULL),
	(6, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'parametrizacion', 'parametro_valor', '6', NULL, NULL, '{"valor": "5", "cod_parametro": "TOLERANCIA_ENTRADA_MIN", "vigente_desde": "2026-01-01", "vigente_hasta": null, "id_parametro_valor": 6}', NULL, NULL),
	(7, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'parametrizacion', 'parametro_valor', '7', NULL, NULL, '{"valor": "15", "cod_parametro": "VACACIONES_DIAS_POR_ANIO", "vigente_desde": "2026-01-01", "vigente_hasta": null, "id_parametro_valor": 7}', NULL, NULL),
	(8, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'parametrizacion', 'parametro_valor', '8', NULL, NULL, '{"valor": "true", "cod_parametro": "CONFLICTO_BLOQUEA", "vigente_desde": "2026-01-01", "vigente_hasta": null, "id_parametro_valor": 8}', NULL, NULL),
	(9, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'turnos', 'turno', '1', NULL, NULL, '{"color": "#F5B041", "activo": true, "nombre": "Mañana", "hora_fin": "14:00:00", "id_turno": 1, "hora_inicio": "06:00:00", "cruza_medianoche": false, "duracion_minutos": 480, "minutos_descanso": 30}', NULL, NULL),
	(10, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'turnos', 'turno', '2', NULL, NULL, '{"color": "#5DADE2", "activo": true, "nombre": "Tarde", "hora_fin": "22:00:00", "id_turno": 2, "hora_inicio": "14:00:00", "cruza_medianoche": false, "duracion_minutos": 480, "minutos_descanso": 30}', NULL, NULL),
	(11, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'turnos', 'turno', '3', NULL, NULL, '{"color": "#34495E", "activo": true, "nombre": "Noche", "hora_fin": "06:00:00", "id_turno": 3, "hora_inicio": "22:00:00", "cruza_medianoche": true, "duracion_minutos": 480, "minutos_descanso": 30}', NULL, NULL),
	(12, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'turnos', 'turno', '4', NULL, NULL, '{"color": "#58D68D", "activo": true, "nombre": "Fin de semana", "hora_fin": "18:00:00", "id_turno": 4, "hora_inicio": "08:00:00", "cruza_medianoche": false, "duracion_minutos": 600, "minutos_descanso": 60}', NULL, NULL),
	(13, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'seguridad', 'rol_permiso', '1', NULL, NULL, '{"id_rol": 1, "id_permiso": 1}', NULL, NULL),
	(14, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'seguridad', 'rol_permiso', '1', NULL, NULL, '{"id_rol": 1, "id_permiso": 2}', NULL, NULL),
	(15, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'seguridad', 'rol_permiso', '1', NULL, NULL, '{"id_rol": 1, "id_permiso": 3}', NULL, NULL),
	(16, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'seguridad', 'rol_permiso', '1', NULL, NULL, '{"id_rol": 1, "id_permiso": 4}', NULL, NULL),
	(17, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'seguridad', 'rol_permiso', '1', NULL, NULL, '{"id_rol": 1, "id_permiso": 5}', NULL, NULL),
	(18, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'seguridad', 'rol_permiso', '1', NULL, NULL, '{"id_rol": 1, "id_permiso": 6}', NULL, NULL),
	(19, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'seguridad', 'rol_permiso', '1', NULL, NULL, '{"id_rol": 1, "id_permiso": 7}', NULL, NULL),
	(20, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'seguridad', 'rol_permiso', '1', NULL, NULL, '{"id_rol": 1, "id_permiso": 8}', NULL, NULL),
	(21, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'seguridad', 'rol_permiso', '1', NULL, NULL, '{"id_rol": 1, "id_permiso": 9}', NULL, NULL),
	(22, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'seguridad', 'rol_permiso', '1', NULL, NULL, '{"id_rol": 1, "id_permiso": 10}', NULL, NULL),
	(23, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'seguridad', 'rol_permiso', '1', NULL, NULL, '{"id_rol": 1, "id_permiso": 11}', NULL, NULL),
	(24, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'seguridad', 'rol_permiso', '1', NULL, NULL, '{"id_rol": 1, "id_permiso": 12}', NULL, NULL),
	(25, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'seguridad', 'rol_permiso', '1', NULL, NULL, '{"id_rol": 1, "id_permiso": 13}', NULL, NULL),
	(26, '2026-11-16 18:40:06.797755-05', NULL, 'CREAR', 'seguridad', 'usuario', '1', NULL, NULL, '{"correo": "admin@tuempresa.com", "id_rol": 1, "creado_en": "2026-11-16T23:40:06.797755+00:00", "id_usuario": 1, "ultimo_acceso": null, "cod_estado_usuario": "ACTIVO"}', NULL, NULL),
	(69, '2026-11-17 14:35:51.602167-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-17T14:35:48.911721-05:00"}', '{"ultimo_acceso": "2026-11-17T14:35:51.652602-05:00"}', NULL, '::1'),
	(71, '2026-11-17 14:45:14.938418-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-17T14:35:51.652602-05:00"}', '{"ultimo_acceso": "2026-11-17T14:45:18.593674-05:00"}', NULL, '::1'),
	(47, '2026-11-17 11:44:53.200755-05', NULL, 'CREAR', 'organizacion', 'sucursal', '5', NULL, NULL, '{"activo": true, "ciudad": "Bogotá", "nombre": "Sede Principal", "direccion": "Calle 10 # 5-23", "id_sucursal": 5}', NULL, NULL),
	(48, '2026-11-17 11:44:53.200755-05', NULL, 'CREAR', 'organizacion', 'area', '5', NULL, NULL, '{"activo": true, "nombre": "Administración", "id_area": 5, "id_sucursal": 5}', NULL, NULL),
	(49, '2026-11-17 11:44:53.200755-05', NULL, 'CREAR', 'organizacion', 'cargo', '5', NULL, NULL, '{"activo": true, "nombre": "Administrador del sistema", "id_cargo": 5}', NULL, NULL),
	(50, '2026-11-17 11:44:53.200755-05', NULL, 'CREAR', 'seguridad', 'usuario', '6', NULL, NULL, '{"correo": "admin@gestionturnos.local", "id_rol": 1, "creado_en": "2026-11-17T11:44:53.200755-05:00", "id_usuario": 6, "ultimo_acceso": null, "cod_estado_usuario": "ACTIVO"}', NULL, NULL),
	(51, '2026-11-17 11:44:53.200755-05', NULL, 'CREAR', 'organizacion', 'empleado', '5', 5, NULL, '{"id_area": 5, "nombres": "Andrés Julián", "id_cargo": 5, "telefono": "3001234567", "apellidos": "Muñoz Peña", "creado_en": "2026-11-17T11:44:53.200755-05:00", "direccion": "Calle 10 # 5-23, Bogotá", "id_usuario": 6, "id_empleado": 5, "fecha_retiro": null, "fecha_ingreso": "2026-01-01", "correo_contacto": "admin@gestionturnos.local", "numero_documento": "1000000001", "cod_tipo_contrato": "INDEFINIDO", "cod_tipo_documento": "CC", "cod_estado_empleado": "ACTIVO"}', NULL, NULL),
	(52, '2026-11-17 11:46:37.251474-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(53, '2026-11-17 11:46:33.420877-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": null}', '{"ultimo_acceso": "2026-11-17T11:46:37.251268-05:00"}', NULL, '::1'),
	(54, '2026-11-17 12:02:42.518088-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(55, '2026-11-17 12:02:42.448716-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-17T11:46:37.251268-05:00"}', '{"ultimo_acceso": "2026-11-17T12:02:42.517812-05:00"}', NULL, '::1'),
	(56, '2026-11-17 12:03:25.001648-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(57, '2026-11-17 12:03:24.936609-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-17T12:02:42.517812-05:00"}', '{"ultimo_acceso": "2026-11-17T12:03:25.001646-05:00"}', NULL, '::1'),
	(58, '2026-11-17 14:09:29.503671-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(59, '2026-11-17 14:09:25.811317-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-17T12:03:25.001646-05:00"}', '{"ultimo_acceso": "2026-11-17T14:09:29.503471-05:00"}', NULL, '::1'),
	(60, '2026-11-17 14:32:46.136753-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(61, '2026-11-17 14:32:46.085559-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-17T14:09:29.503471-05:00"}', '{"ultimo_acceso": "2026-11-17T14:32:46.136305-05:00"}', NULL, '::1'),
	(62, '2026-11-17 14:33:09.495347-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(63, '2026-11-17 14:33:09.445372-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-17T14:32:46.136305-05:00"}', '{"ultimo_acceso": "2026-11-17T14:33:09.495328-05:00"}', NULL, '::1'),
	(64, '2026-11-17 14:33:50.254104-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(65, '2026-11-17 14:33:50.205715-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-17T14:33:09.495328-05:00"}', '{"ultimo_acceso": "2026-11-17T14:33:50.254102-05:00"}', NULL, '::1'),
	(66, '2026-11-17 14:35:48.9119-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(67, '2026-11-17 14:35:48.862236-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-17T14:33:50.254102-05:00"}', '{"ultimo_acceso": "2026-11-17T14:35:48.911721-05:00"}', NULL, '::1'),
	(68, '2026-11-17 14:35:51.652604-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(70, '2026-11-17 14:45:18.593833-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(72, '2026-11-17 14:53:02.190271-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(73, '2026-11-17 14:53:02.14636-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-17T14:45:18.593674-05:00"}', '{"ultimo_acceso": "2026-11-17T14:53:02.19001-05:00"}', NULL, '::1'),
	(74, '2026-11-17 15:41:53.589219-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(75, '2026-11-17 15:41:49.994553-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-17T14:53:02.19001-05:00"}', '{"ultimo_acceso": "2026-11-17T15:41:53.589217-05:00"}', NULL, '::1'),
	(76, '2026-11-17 16:26:07.301045-05', NULL, 'CREAR', 'seguridad', 'solicitud_registro', '1', NULL, NULL, '{"correo": "samueldiazp.dev@gmail.com", "nombres": "Samuel", "telefono": "3106653490", "apellidos": "Díaz Pérez", "revisado_en": null, "revisado_por": null, "solicitado_en": "2026-11-17T16:26:10.523968-05:00", "motivo_rechazo": null, "numero_documento": "1033487560", "id_usuario_creado": null, "cod_tipo_documento": "CC", "cod_estado_solicitud": "PENDIENTE", "id_solicitud_registro": 1}', NULL, '::1'),
	(77, '2026-11-17 16:28:45.828341-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(78, '2026-11-17 16:28:42.27697-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-17T15:41:53.589217-05:00"}', '{"ultimo_acceso": "2026-11-17T16:28:45.827929-05:00"}', NULL, '::1'),
	(79, '2026-11-17 17:02:51.01757-05', 6, 'CREAR', 'seguridad', 'usuario', '7', NULL, NULL, '{"correo": "samueldiazp.dev@gmail.com", "id_rol": 2, "creado_en": "2026-11-17T17:02:51.524952-05:00", "id_usuario": 7, "ultimo_acceso": null, "cod_estado_usuario": "ACTIVO"}', NULL, '::1'),
	(80, '2026-11-17 17:02:52.065538-05', 6, 'CREAR', 'seguridad', 'usuario', '7', NULL, NULL, '{"IdRol": 2, "Correo": "samueldiazp.dev@gmail.com", "CreadoEn": "2026-11-17T22:02:51.5249529+00:00", "IdUsuario": -9223372036854774807, "CodEstadoUsuario": "ACTIVO"}', NULL, '::1'),
	(81, '2026-11-17 17:02:51.01757-05', 6, 'CREAR', 'organizacion', 'empleado', '6', 6, NULL, '{"id_area": 5, "nombres": "Samuel", "id_cargo": 5, "telefono": "3106653490", "apellidos": "Díaz Pérez", "creado_en": "2026-11-17T17:02:52.390491-05:00", "direccion": null, "id_usuario": 7, "id_empleado": 6, "fecha_retiro": null, "fecha_ingreso": "2026-11-17", "correo_contacto": "samueldiazp.dev@gmail.com", "numero_documento": "1033487560", "cod_tipo_contrato": "FIJO", "cod_tipo_documento": "CC", "cod_estado_empleado": "ACTIVO"}', NULL, '::1'),
	(82, '2026-11-17 17:02:52.73262-05', 6, 'CREAR', 'organizacion', 'empleado', '6', 6, NULL, '{"IdArea": 5, "IdCargo": 5, "Nombres": "Samuel", "CreadoEn": "2026-11-17T22:02:52.3904918+00:00", "Telefono": "3106653490", "Apellidos": "Díaz Pérez", "IdUsuario": 7, "IdEmpleado": -9223372036854774807, "FechaIngreso": "2026-11-17", "CorreoContacto": "samueldiazp.dev@gmail.com", "CodTipoContrato": "FIJO", "NumeroDocumento": "1033487560", "CodTipoDocumento": "CC", "CodEstadoEmpleado": "ACTIVO"}', NULL, '::1'),
	(83, '2026-11-17 17:02:51.01757-05', 6, 'MODIFICAR', 'seguridad', 'solicitud_registro', '1', NULL, '{"revisado_en": null, "revisado_por": null, "id_usuario_creado": null, "cod_estado_solicitud": "PENDIENTE"}', '{"revisado_en": "2026-11-17T17:02:52.982145-05:00", "revisado_por": 6, "id_usuario_creado": 7, "cod_estado_solicitud": "APROBADA"}', NULL, '::1'),
	(84, '2026-11-17 17:02:53.364618-05', 6, 'MODIFICAR', 'seguridad', 'solicitud_registro', '1', NULL, '{"RevisadoEn": null, "RevisadoPor": null, "IdUsuarioCreado": null, "CodEstadoSolicitud": "PENDIENTE"}', '{"RevisadoEn": "2026-11-17T22:02:52.9821459+00:00", "RevisadoPor": 6, "IdUsuarioCreado": 7, "CodEstadoSolicitud": "APROBADA"}', NULL, '::1'),
	(85, '2026-11-17 17:03:05.197298-05', 6, 'MODIFICAR', 'organizacion', 'empleado', '6', 6, '{"cod_tipo_contrato": "FIJO"}', '{"cod_tipo_contrato": "INDEFINIDO"}', NULL, '::1'),
	(86, '2026-11-17 17:03:05.73698-05', 6, 'MODIFICAR', 'organizacion', 'empleado', '6', 6, '{"CodTipoContrato": "FIJO"}', '{"CodTipoContrato": "INDEFINIDO"}', NULL, '::1'),
	(87, '2026-11-17 17:19:38.045469-05', 7, 'INICIO_SESION', 'seguridad', 'usuario', '7', 6, NULL, NULL, NULL, '::1'),
	(88, '2026-11-17 17:19:38.027481-05', 7, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"ultimo_acceso": null}', '{"ultimo_acceso": "2026-11-17T17:19:38.045086-05:00"}', NULL, '::1'),
	(89, '2026-11-17 17:19:38.517927-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"UltimoAcceso": null}', '{"UltimoAcceso": "2026-11-17T22:19:38.0450868+00:00"}', NULL, '::1'),
	(90, '2026-11-18 07:41:15.16602-05', 7, 'INICIO_SESION', 'seguridad', 'usuario', '7', 6, NULL, NULL, NULL, '::1'),
	(91, '2026-11-18 07:41:15.141729-05', 7, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"ultimo_acceso": "2026-11-17T17:19:38.045086-05:00"}', '{"ultimo_acceso": "2026-11-18T07:41:15.165818-05:00"}', NULL, '::1'),
	(92, '2026-11-18 07:41:15.654268-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"UltimoAcceso": "2026-11-17T22:19:38.045086+00:00"}', '{"UltimoAcceso": "2026-11-18T12:41:15.1658187+00:00"}', NULL, '::1'),
	(93, '2026-11-18 07:42:03.623624-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(94, '2026-11-18 07:42:03.603602-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-17T16:28:45.827929-05:00"}', '{"ultimo_acceso": "2026-11-18T07:42:03.623623-05:00"}', NULL, '::1'),
	(95, '2026-11-18 07:42:03.857525-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-17T21:28:45.827929+00:00"}', '{"UltimoAcceso": "2026-11-18T12:42:03.6236232+00:00"}', NULL, '::1'),
	(96, '2026-11-18 07:42:40.528819-05', 6, 'CREAR', 'turnos', 'asignacion', '1', 6, NULL, '{"fecha": "2026-11-16", "periodo": "[\"2026-11-16 06:00:00-05\",\"2026-11-16 14:00:00-05\")", "id_turno": 1, "creado_en": "2026-11-18T07:42:41.50702-05:00", "creado_por": 6, "id_empleado": 6, "observacion": null, "id_asignacion": 1, "cod_estado_asignacion": "PROGRAMADA"}', NULL, '::1'),
	(97, '2026-11-18 07:42:41.909017-05', 6, 'CREAR', 'turnos', 'asignacion', '1', 6, NULL, '{"Fecha": "2026-11-16", "IdTurno": 1, "Periodo": {"Fin": "2026-11-16T14:00:00-05:00", "Inicio": "2026-11-16T06:00:00-05:00", "DuracionMinutos": 480}, "CreadoEn": "2026-11-18T12:42:41.5070203+00:00", "CreadoPor": 6, "IdEmpleado": 6, "IdAsignacion": -9223372036854774807, "CodEstadoAsignacion": "PROGRAMADA"}', NULL, '::1'),
	(98, '2026-11-18 07:42:40.528819-05', 6, 'CREAR', 'turnos', 'asignacion', '2', 6, NULL, '{"fecha": "2026-11-17", "periodo": "[\"2026-11-17 06:00:00-05\",\"2026-11-17 14:00:00-05\")", "id_turno": 1, "creado_en": "2026-11-18T07:42:42.901471-05:00", "creado_por": 6, "id_empleado": 6, "observacion": null, "id_asignacion": 2, "cod_estado_asignacion": "PROGRAMADA"}', NULL, '::1'),
	(99, '2026-11-18 07:42:43.123927-05', 6, 'CREAR', 'turnos', 'asignacion', '2', 6, NULL, '{"Fecha": "2026-11-17", "IdTurno": 1, "Periodo": {"Fin": "2026-11-17T14:00:00-05:00", "Inicio": "2026-11-17T06:00:00-05:00", "DuracionMinutos": 480}, "CreadoEn": "2026-11-18T12:42:42.9014711+00:00", "CreadoPor": 6, "IdEmpleado": 6, "IdAsignacion": -9223372036854774806, "CodEstadoAsignacion": "PROGRAMADA"}', NULL, '::1'),
	(100, '2026-11-18 07:42:40.528819-05', 6, 'CREAR', 'turnos', 'asignacion', '3', 6, NULL, '{"fecha": "2026-11-18", "periodo": "[\"2026-11-18 06:00:00-05\",\"2026-11-18 14:00:00-05\")", "id_turno": 1, "creado_en": "2026-11-18T07:42:44.102396-05:00", "creado_por": 6, "id_empleado": 6, "observacion": null, "id_asignacion": 3, "cod_estado_asignacion": "PROGRAMADA"}', NULL, '::1'),
	(101, '2026-11-18 07:42:44.324112-05', 6, 'CREAR', 'turnos', 'asignacion', '3', 6, NULL, '{"Fecha": "2026-11-18", "IdTurno": 1, "Periodo": {"Fin": "2026-11-18T14:00:00-05:00", "Inicio": "2026-11-18T06:00:00-05:00", "DuracionMinutos": 480}, "CreadoEn": "2026-11-18T12:42:44.1023965+00:00", "CreadoPor": 6, "IdEmpleado": 6, "IdAsignacion": -9223372036854774805, "CodEstadoAsignacion": "PROGRAMADA"}', NULL, '::1'),
	(102, '2026-11-18 07:42:40.528819-05', 6, 'CREAR', 'turnos', 'asignacion', '4', 6, NULL, '{"fecha": "2026-11-19", "periodo": "[\"2026-11-19 06:00:00-05\",\"2026-11-19 14:00:00-05\")", "id_turno": 1, "creado_en": "2026-11-18T07:42:45.36969-05:00", "creado_por": 6, "id_empleado": 6, "observacion": null, "id_asignacion": 4, "cod_estado_asignacion": "PROGRAMADA"}', NULL, '::1'),
	(103, '2026-11-18 07:42:45.590682-05', 6, 'CREAR', 'turnos', 'asignacion', '4', 6, NULL, '{"Fecha": "2026-11-19", "IdTurno": 1, "Periodo": {"Fin": "2026-11-19T14:00:00-05:00", "Inicio": "2026-11-19T06:00:00-05:00", "DuracionMinutos": 480}, "CreadoEn": "2026-11-18T12:42:45.3696909+00:00", "CreadoPor": 6, "IdEmpleado": 6, "IdAsignacion": -9223372036854774804, "CodEstadoAsignacion": "PROGRAMADA"}', NULL, '::1'),
	(104, '2026-11-18 07:42:40.528819-05', 6, 'CREAR', 'turnos', 'asignacion', '5', 6, NULL, '{"fecha": "2026-11-20", "periodo": "[\"2026-11-20 06:00:00-05\",\"2026-11-20 14:00:00-05\")", "id_turno": 1, "creado_en": "2026-11-18T07:42:46.574916-05:00", "creado_por": 6, "id_empleado": 6, "observacion": null, "id_asignacion": 5, "cod_estado_asignacion": "PROGRAMADA"}', NULL, '::1'),
	(105, '2026-11-18 07:42:46.797047-05', 6, 'CREAR', 'turnos', 'asignacion', '5', 6, NULL, '{"Fecha": "2026-11-20", "IdTurno": 1, "Periodo": {"Fin": "2026-11-20T14:00:00-05:00", "Inicio": "2026-11-20T06:00:00-05:00", "DuracionMinutos": 480}, "CreadoEn": "2026-11-18T12:42:46.5749161+00:00", "CreadoPor": 6, "IdEmpleado": 6, "IdAsignacion": -9223372036854774803, "CodEstadoAsignacion": "PROGRAMADA"}', NULL, '::1'),
	(106, '2026-11-18 07:43:02.039994-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '1', 6, '{"cod_estado_asignacion": "PROGRAMADA"}', '{"cod_estado_asignacion": "CONFIRMADA"}', NULL, '::1'),
	(107, '2026-11-18 07:43:02.039994-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '2', 6, '{"cod_estado_asignacion": "PROGRAMADA"}', '{"cod_estado_asignacion": "CONFIRMADA"}', NULL, '::1'),
	(108, '2026-11-18 07:43:02.039994-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '3', 6, '{"cod_estado_asignacion": "PROGRAMADA"}', '{"cod_estado_asignacion": "CONFIRMADA"}', NULL, '::1'),
	(109, '2026-11-18 07:43:02.039994-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '4', 6, '{"cod_estado_asignacion": "PROGRAMADA"}', '{"cod_estado_asignacion": "CONFIRMADA"}', NULL, '::1'),
	(110, '2026-11-18 07:43:02.039994-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '5', 6, '{"cod_estado_asignacion": "PROGRAMADA"}', '{"cod_estado_asignacion": "CONFIRMADA"}', NULL, '::1'),
	(111, '2026-11-18 07:43:02.292221-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '1', 6, '{"CodEstadoAsignacion": "PROGRAMADA"}', '{"CodEstadoAsignacion": "CONFIRMADA"}', NULL, '::1'),
	(112, '2026-11-18 07:43:02.292366-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '2', 6, '{"CodEstadoAsignacion": "PROGRAMADA"}', '{"CodEstadoAsignacion": "CONFIRMADA"}', NULL, '::1'),
	(113, '2026-11-18 07:43:02.292413-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '3', 6, '{"CodEstadoAsignacion": "PROGRAMADA"}', '{"CodEstadoAsignacion": "CONFIRMADA"}', NULL, '::1'),
	(114, '2026-11-18 07:43:02.292452-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '4', 6, '{"CodEstadoAsignacion": "PROGRAMADA"}', '{"CodEstadoAsignacion": "CONFIRMADA"}', NULL, '::1'),
	(115, '2026-11-18 07:43:02.2925-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '5', 6, '{"CodEstadoAsignacion": "PROGRAMADA"}', '{"CodEstadoAsignacion": "CONFIRMADA"}', NULL, '::1'),
	(116, '2026-11-18 07:43:48.868224-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(117, '2026-11-18 07:43:48.727591-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-18T07:42:03.623623-05:00"}', '{"ultimo_acceso": "2026-11-18T07:43:48.867836-05:00"}', NULL, '::1'),
	(118, '2026-11-18 07:43:49.536624-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-18T12:42:03.623623+00:00"}', '{"UltimoAcceso": "2026-11-18T12:43:48.8678366+00:00"}', NULL, '::1'),
	(119, '2026-11-18 07:43:50.210128-05', 7, 'INICIO_SESION', 'seguridad', 'usuario', '7', 6, NULL, NULL, NULL, '::1'),
	(120, '2026-11-18 07:43:50.189726-05', 7, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"ultimo_acceso": "2026-11-18T07:41:15.165818-05:00"}', '{"ultimo_acceso": "2026-11-18T07:43:50.210127-05:00"}', NULL, '::1'),
	(121, '2026-11-18 07:43:50.453928-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"UltimoAcceso": "2026-11-18T12:41:15.165818+00:00"}', '{"UltimoAcceso": "2026-11-18T12:43:50.210127+00:00"}', NULL, '::1'),
	(122, '2026-11-18 07:44:01.4006-05', 7, 'CREAR', 'asistencia', 'registro', '1', 6, NULL, '{"creado_en": "2026-11-18T07:44:01.102323-05:00", "hora_salida": null, "id_empleado": 6, "id_registro": 1, "observacion": null, "hora_entrada": "2026-11-18T07:44:01.102323-05:00", "id_asignacion": 3, "registrado_por": null, "cod_fuente_marcacion": "WEB"}', NULL, '::1'),
	(123, '2026-11-18 07:44:01.674014-05', 7, 'CREAR', 'asistencia', 'registro', '1', 6, NULL, '{"CreadoEn": "2026-11-18T12:44:01.1023232+00:00", "IdEmpleado": 6, "IdRegistro": -9223372036854774807, "HoraEntrada": "2026-11-18T12:44:01.1023232+00:00", "IdAsignacion": 3, "CodFuenteMarcacion": "WEB"}', NULL, '::1'),
	(124, '2026-11-18 08:03:02.503903-05', 7, 'CREAR', 'novedades', 'novedad', '1', 6, NULL, '{"fin": "2026-11-17T14:00:00-05:00", "inicio": "2026-11-16T06:00:00-05:00", "id_novedad": 1, "remunerado": true, "id_empleado": 6, "resuelto_en": null, "url_soporte": "https://www.puertocarreno-vichada.gov.co/Transparencia/finanzas_municipales/FACTURA%20001%20-%2036274845%20-%202026.pdf", "afecta_horas": true, "resuelto_por": null, "observaciones": "Tuve una crisis de Migraña y me enviaron incapacidad medica de 2 días", "registrado_en": "2026-11-18T08:03:02.311126-05:00", "motivo_rechazo": null, "registrado_por": 7, "cod_tipo_novedad": "INCAPACIDAD", "cod_estado_solicitud": "PENDIENTE"}', NULL, '::1'),
	(154, '2026-11-18 11:25:02.721312-05', NULL, 'CREAR', 'seguridad', 'solicitud_registro', '2', NULL, NULL, '{"Correo": "samueldiazperez4c@gmail.com", "Nombres": "Juan Sebastián", "Telefono": "3106653493", "Apellidos": "Montaño Pérez", "SolicitadoEn": "2026-11-18T16:25:02.2182938+00:00", "NumeroDocumento": "1022483250", "CodTipoDocumento": "CC", "CodEstadoSolicitud": "PENDIENTE", "IdSolicitudRegistro": -9223372036854774807}', NULL, '::1'),
	(125, '2026-11-18 08:03:02.825659-05', 7, 'CREAR', 'novedades', 'novedad', '1', 6, NULL, '{"Fin": "2026-11-17T14:00:00-05:00", "Inicio": "2026-11-16T06:00:00-05:00", "IdNovedad": -9223372036854774807, "IdEmpleado": 6, "Remunerado": true, "UrlSoporte": "https://www.puertocarreno-vichada.gov.co/Transparencia/finanzas_municipales/FACTURA%20001%20-%2036274845%20-%202026.pdf", "AfectaHoras": true, "RegistradoEn": "2026-11-18T13:03:02.3111268+00:00", "Observaciones": "Tuve una crisis de Migraña y me enviaron incapacidad medica de 2 días", "RegistradoPor": 7, "CodTipoNovedad": "INCAPACIDAD", "CodEstadoSolicitud": "PENDIENTE"}', NULL, '::1'),
	(126, '2026-11-18 08:03:39.411606-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(127, '2026-11-18 08:03:39.400437-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-18T07:43:48.867836-05:00"}', '{"ultimo_acceso": "2026-11-18T08:03:39.411604-05:00"}', NULL, '::1'),
	(128, '2026-11-18 08:03:39.683668-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-18T12:43:48.867836+00:00"}', '{"UltimoAcceso": "2026-11-18T13:03:39.4116049+00:00"}', NULL, '::1'),
	(129, '2026-11-18 08:04:20.71867-05', 6, 'MODIFICAR', 'novedades', 'novedad', '1', 6, '{"resuelto_en": null, "resuelto_por": null, "cod_estado_solicitud": "PENDIENTE"}', '{"resuelto_en": "2026-11-18T08:04:21.125806-05:00", "resuelto_por": 6, "cod_estado_solicitud": "APROBADA"}', NULL, '::1'),
	(130, '2026-11-18 08:04:21.394475-05', 6, 'MODIFICAR', 'novedades', 'novedad', '1', 6, '{"ResueltoEn": null, "ResueltoPor": null, "CodEstadoSolicitud": "PENDIENTE"}', '{"ResueltoEn": "2026-11-18T13:04:21.1258069+00:00", "ResueltoPor": 6, "CodEstadoSolicitud": "APROBADA"}', NULL, '::1'),
	(131, '2026-11-18 08:04:35.70274-05', 7, 'INICIO_SESION', 'seguridad', 'usuario', '7', 6, NULL, NULL, NULL, '::1'),
	(132, '2026-11-18 08:04:35.693093-05', 7, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"ultimo_acceso": "2026-11-18T07:43:50.210127-05:00"}', '{"ultimo_acceso": "2026-11-18T08:04:35.702739-05:00"}', NULL, '::1'),
	(133, '2026-11-18 08:04:35.967763-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"UltimoAcceso": "2026-11-18T12:43:50.210127+00:00"}', '{"UltimoAcceso": "2026-11-18T13:04:35.702739+00:00"}', NULL, '::1'),
	(134, '2026-11-18 09:01:53.279084-05', 7, 'MODIFICAR', 'seguridad', 'usuario', '7', 6, NULL, NULL, 'Cambio de contraseña', '::1'),
	(135, '2026-11-18 09:03:17.425632-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(136, '2026-11-18 09:03:16.59171-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-18T08:03:39.411604-05:00"}', '{"ultimo_acceso": "2026-11-18T09:03:17.425484-05:00"}', NULL, '::1'),
	(137, '2026-11-18 09:03:17.794574-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-18T13:03:39.411604+00:00"}', '{"UltimoAcceso": "2026-11-18T14:03:17.425484+00:00"}', NULL, '::1'),
	(138, '2026-11-18 09:38:12.389659-05', 6, 'MODIFICAR', 'asistencia', 'registro', '1', 6, '{"observacion": null, "hora_entrada": "2026-11-18T07:44:01.102323-05:00", "registrado_por": null, "cod_fuente_marcacion": "WEB"}', '{"observacion": "Tenia una reunión a las 6:20 a.m.  y no realizó el marcaje recién llegó", "hora_entrada": "2026-11-18T06:18:00-05:00", "registrado_por": 6, "cod_fuente_marcacion": "ADMIN"}', NULL, '::1'),
	(139, '2026-11-18 09:38:14.286881-05', 6, 'MODIFICAR', 'asistencia', 'registro', '1', 6, '{"HoraEntrada": "2026-11-18T12:44:01.102323+00:00", "Observacion": null, "RegistradoPor": null, "CodFuenteMarcacion": "WEB"}', '{"HoraEntrada": "2026-11-18T11:18:00+00:00", "Observacion": "Tenia una reunión a las 6:20 a.m.  y no realizó el marcaje recién llegó", "RegistradoPor": 6, "CodFuenteMarcacion": "ADMIN"}', NULL, '::1'),
	(140, '2026-11-18 09:38:41.605739-05', 7, 'INICIO_SESION', 'seguridad', 'usuario', '7', 6, NULL, NULL, NULL, '::1'),
	(141, '2026-11-18 09:38:40.850439-05', 7, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"ultimo_acceso": "2026-11-18T08:04:35.702739-05:00"}', '{"ultimo_acceso": "2026-11-18T09:38:41.605738-05:00"}', NULL, '::1'),
	(142, '2026-11-18 09:38:41.863582-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"UltimoAcceso": "2026-11-18T13:04:35.702739+00:00"}', '{"UltimoAcceso": "2026-11-18T14:38:41.6057383+00:00"}', NULL, '::1'),
	(143, '2026-11-18 10:20:52.028481-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(144, '2026-11-18 10:20:51.295103-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-18T09:03:17.425484-05:00"}', '{"ultimo_acceso": "2026-11-18T10:20:52.02795-05:00"}', NULL, '::1'),
	(145, '2026-11-18 10:20:52.587194-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-18T14:03:17.425484+00:00"}', '{"UltimoAcceso": "2026-11-18T15:20:52.0279508+00:00"}', NULL, '::1'),
	(146, '2026-11-18 10:31:15.632582-05', 6, 'CREAR', 'turnos', 'asignacion', '6', 5, NULL, '{"fecha": "2026-11-16", "periodo": "[\"2026-11-16 06:00:00-05\",\"2026-11-16 14:00:00-05\")", "id_turno": 1, "creado_en": "2026-11-18T10:31:17.13985-05:00", "creado_por": 6, "id_empleado": 5, "observacion": null, "id_asignacion": 6, "cod_estado_asignacion": "PROGRAMADA"}', NULL, '::1'),
	(147, '2026-11-18 10:31:17.594045-05', 6, 'CREAR', 'turnos', 'asignacion', '6', 5, NULL, '{"Fecha": "2026-11-16", "IdTurno": 1, "Periodo": {"Fin": "2026-11-16T14:00:00-05:00", "Inicio": "2026-11-16T06:00:00-05:00", "DuracionMinutos": 480}, "CreadoEn": "2026-11-18T15:31:17.1398502+00:00", "CreadoPor": 6, "IdEmpleado": 5, "IdAsignacion": -9223372036854774807, "CodEstadoAsignacion": "PROGRAMADA"}', NULL, '::1'),
	(148, '2026-11-18 11:23:16.162524-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(149, '2026-11-18 11:23:15.506887-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-18T10:20:52.02795-05:00"}', '{"ultimo_acceso": "2026-11-18T11:23:16.162075-05:00"}', NULL, '::1'),
	(150, '2026-11-18 11:23:16.687305-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-18T15:20:52.02795+00:00"}', '{"UltimoAcceso": "2026-11-18T16:23:16.1620752+00:00"}', NULL, '::1'),
	(151, '2026-11-18 11:24:39.698386-05', 6, 'MODIFICAR', 'organizacion', 'empleado', '6', 6, '{"correo_contacto": "samueldiazp.dev@gmail.com"}', '{"correo_contacto": "samueldiazp@gmail.com"}', NULL, '::1'),
	(152, '2026-11-18 11:24:41.145541-05', 6, 'MODIFICAR', 'organizacion', 'empleado', '6', 6, '{"CorreoContacto": "samueldiazp.dev@gmail.com"}', '{"CorreoContacto": "samueldiazp@gmail.com"}', NULL, '::1'),
	(153, '2026-11-18 11:25:01.737696-05', NULL, 'CREAR', 'seguridad', 'solicitud_registro', '2', NULL, NULL, '{"correo": "samueldiazperez4c@gmail.com", "nombres": "Juan Sebastián", "telefono": "3106653493", "apellidos": "Montaño Pérez", "revisado_en": null, "revisado_por": null, "solicitado_en": "2026-11-18T11:25:02.218293-05:00", "motivo_rechazo": null, "numero_documento": "1022483250", "id_usuario_creado": null, "cod_tipo_documento": "CC", "cod_estado_solicitud": "PENDIENTE", "id_solicitud_registro": 2}', NULL, '::1'),
	(155, '2026-11-18 11:25:14.113451-05', 6, 'MODIFICAR', 'organizacion', 'empleado', '6', 6, '{"correo_contacto": "samueldiazp@gmail.com"}', '{"correo_contacto": "samueldiazp.dev@gmail.com"}', NULL, '::1'),
	(156, '2026-11-18 11:25:15.135856-05', 6, 'MODIFICAR', 'organizacion', 'empleado', '6', 6, '{"CorreoContacto": "samueldiazp@gmail.com"}', '{"CorreoContacto": "samueldiazp.dev@gmail.com"}', NULL, '::1'),
	(157, '2026-11-18 11:25:41.172628-05', 6, 'CREAR', 'seguridad', 'usuario', '8', NULL, NULL, '{"correo": "samueldiazperez4c@gmail.com", "id_rol": 2, "creado_en": "2026-11-18T11:25:42.015438-05:00", "id_usuario": 8, "ultimo_acceso": null, "cod_estado_usuario": "ACTIVO"}', NULL, '::1'),
	(158, '2026-11-18 11:25:42.225326-05', 6, 'CREAR', 'seguridad', 'usuario', '8', NULL, NULL, '{"IdRol": 2, "Correo": "samueldiazperez4c@gmail.com", "CreadoEn": "2026-11-18T16:25:42.0154382+00:00", "IdUsuario": -9223372036854774807, "CodEstadoUsuario": "ACTIVO"}', NULL, '::1'),
	(159, '2026-11-18 11:25:41.172628-05', 6, 'CREAR', 'organizacion', 'empleado', '7', 7, NULL, '{"id_area": 5, "nombres": "Juan Sebastián", "id_cargo": 5, "telefono": "3106653493", "apellidos": "Montaño Pérez", "creado_en": "2026-11-18T11:25:42.446835-05:00", "direccion": null, "id_usuario": 8, "id_empleado": 7, "fecha_retiro": null, "fecha_ingreso": "2026-11-18", "correo_contacto": "samueldiazperez4c@gmail.com", "numero_documento": "1022483250", "cod_tipo_contrato": "INDEFINIDO", "cod_tipo_documento": "CC", "cod_estado_empleado": "ACTIVO"}', NULL, '::1'),
	(160, '2026-11-18 11:25:42.667931-05', 6, 'CREAR', 'organizacion', 'empleado', '7', 7, NULL, '{"IdArea": 5, "IdCargo": 5, "Nombres": "Juan Sebastián", "CreadoEn": "2026-11-18T16:25:42.4468356+00:00", "Telefono": "3106653493", "Apellidos": "Montaño Pérez", "IdUsuario": 8, "IdEmpleado": -9223372036854774807, "FechaIngreso": "2026-11-18", "CorreoContacto": "samueldiazperez4c@gmail.com", "CodTipoContrato": "INDEFINIDO", "NumeroDocumento": "1022483250", "CodTipoDocumento": "CC", "CodEstadoEmpleado": "ACTIVO"}', NULL, '::1'),
	(161, '2026-11-18 11:25:41.172628-05', 6, 'MODIFICAR', 'seguridad', 'solicitud_registro', '2', NULL, '{"revisado_en": null, "revisado_por": null, "id_usuario_creado": null, "cod_estado_solicitud": "PENDIENTE"}', '{"revisado_en": "2026-11-18T11:25:42.854138-05:00", "revisado_por": 6, "id_usuario_creado": 8, "cod_estado_solicitud": "APROBADA"}', NULL, '::1'),
	(162, '2026-11-18 11:25:43.047178-05', 6, 'MODIFICAR', 'seguridad', 'solicitud_registro', '2', NULL, '{"RevisadoEn": null, "RevisadoPor": null, "IdUsuarioCreado": null, "CodEstadoSolicitud": "PENDIENTE"}', '{"RevisadoEn": "2026-11-18T16:25:42.8541382+00:00", "RevisadoPor": 6, "IdUsuarioCreado": 8, "CodEstadoSolicitud": "APROBADA"}', NULL, '::1'),
	(163, '2026-11-18 11:40:43.718972-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '6', 5, '{"observacion": null, "cod_estado_asignacion": "PROGRAMADA"}', '{"observacion": "Prueba de anulación", "cod_estado_asignacion": "ANULADA"}', NULL, '::1'),
	(164, '2026-11-18 11:40:44.58731-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '6', 5, '{"Observacion": null, "CodEstadoAsignacion": "PROGRAMADA"}', '{"Observacion": "Prueba de anulación", "CodEstadoAsignacion": "ANULADA"}', NULL, '::1'),
	(165, '2026-11-18 11:41:20.095946-05', 6, 'CREAR', 'turnos', 'asignacion', '7', 7, NULL, '{"fecha": "2026-11-18", "periodo": "[\"2026-11-18 14:00:00-05\",\"2026-11-18 22:00:00-05\")", "id_turno": 2, "creado_en": "2026-11-18T11:41:21.674395-05:00", "creado_por": 6, "id_empleado": 7, "observacion": "Debe de recibirle el turno a Samuel Díaz", "id_asignacion": 7, "cod_estado_asignacion": "PROGRAMADA"}', NULL, '::1'),
	(166, '2026-11-18 11:41:21.915635-05', 6, 'CREAR', 'turnos', 'asignacion', '7', 7, NULL, '{"Fecha": "2026-11-18", "IdTurno": 2, "Periodo": {"Fin": "2026-11-18T22:00:00-05:00", "Inicio": "2026-11-18T14:00:00-05:00", "DuracionMinutos": 480}, "CreadoEn": "2026-11-18T16:41:21.6743951+00:00", "CreadoPor": 6, "IdEmpleado": 7, "Observacion": "Debe de recibirle el turno a Samuel Díaz", "IdAsignacion": -9223372036854774807, "CodEstadoAsignacion": "PROGRAMADA"}', NULL, '::1'),
	(167, '2026-11-18 11:41:20.095946-05', 6, 'CREAR', 'turnos', 'asignacion', '8', 7, NULL, '{"fecha": "2026-11-19", "periodo": "[\"2026-11-19 14:00:00-05\",\"2026-11-19 22:00:00-05\")", "id_turno": 2, "creado_en": "2026-11-18T11:41:22.909822-05:00", "creado_por": 6, "id_empleado": 7, "observacion": "Debe de recibirle el turno a Samuel Díaz", "id_asignacion": 8, "cod_estado_asignacion": "PROGRAMADA"}', NULL, '::1'),
	(168, '2026-11-18 11:41:23.290207-05', 6, 'CREAR', 'turnos', 'asignacion', '8', 7, NULL, '{"Fecha": "2026-11-19", "IdTurno": 2, "Periodo": {"Fin": "2026-11-19T22:00:00-05:00", "Inicio": "2026-11-19T14:00:00-05:00", "DuracionMinutos": 480}, "CreadoEn": "2026-11-18T16:41:22.9098222+00:00", "CreadoPor": 6, "IdEmpleado": 7, "Observacion": "Debe de recibirle el turno a Samuel Díaz", "IdAsignacion": -9223372036854774806, "CodEstadoAsignacion": "PROGRAMADA"}', NULL, '::1'),
	(169, '2026-11-18 11:41:20.095946-05', 6, 'CREAR', 'turnos', 'asignacion', '9', 7, NULL, '{"fecha": "2026-11-20", "periodo": "[\"2026-11-20 14:00:00-05\",\"2026-11-20 22:00:00-05\")", "id_turno": 2, "creado_en": "2026-11-18T11:41:24.371789-05:00", "creado_por": 6, "id_empleado": 7, "observacion": "Debe de recibirle el turno a Samuel Díaz", "id_asignacion": 9, "cod_estado_asignacion": "PROGRAMADA"}', NULL, '::1'),
	(170, '2026-11-18 11:41:24.609178-05', 6, 'CREAR', 'turnos', 'asignacion', '9', 7, NULL, '{"Fecha": "2026-11-20", "IdTurno": 2, "Periodo": {"Fin": "2026-11-20T22:00:00-05:00", "Inicio": "2026-11-20T14:00:00-05:00", "DuracionMinutos": 480}, "CreadoEn": "2026-11-18T16:41:24.3717898+00:00", "CreadoPor": 6, "IdEmpleado": 7, "Observacion": "Debe de recibirle el turno a Samuel Díaz", "IdAsignacion": -9223372036854774805, "CodEstadoAsignacion": "PROGRAMADA"}', NULL, '::1'),
	(171, '2026-11-18 11:43:02.595934-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '7', 7, '{"cod_estado_asignacion": "PROGRAMADA"}', '{"cod_estado_asignacion": "CONFIRMADA"}', NULL, '::1'),
	(172, '2026-11-18 11:43:02.595934-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '8', 7, '{"cod_estado_asignacion": "PROGRAMADA"}', '{"cod_estado_asignacion": "CONFIRMADA"}', NULL, '::1'),
	(173, '2026-11-18 11:43:02.595934-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '9', 7, '{"cod_estado_asignacion": "PROGRAMADA"}', '{"cod_estado_asignacion": "CONFIRMADA"}', NULL, '::1'),
	(174, '2026-11-18 11:43:03.663947-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '7', 7, '{"CodEstadoAsignacion": "PROGRAMADA"}', '{"CodEstadoAsignacion": "CONFIRMADA"}', NULL, '::1'),
	(175, '2026-11-18 11:43:03.664136-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '8', 7, '{"CodEstadoAsignacion": "PROGRAMADA"}', '{"CodEstadoAsignacion": "CONFIRMADA"}', NULL, '::1'),
	(176, '2026-11-18 11:43:03.664193-05', 6, 'MODIFICAR', 'turnos', 'asignacion', '9', 7, '{"CodEstadoAsignacion": "PROGRAMADA"}', '{"CodEstadoAsignacion": "CONFIRMADA"}', NULL, '::1'),
	(177, '2026-11-18 13:41:36.539046-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(178, '2026-11-18 13:41:36.024861-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-18T11:23:16.162075-05:00"}', '{"ultimo_acceso": "2026-11-18T13:41:36.538837-05:00"}', NULL, '::1'),
	(179, '2026-11-18 13:41:36.973852-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-18T16:23:16.162075+00:00"}', '{"UltimoAcceso": "2026-11-18T18:41:36.5388372+00:00"}', NULL, '::1'),
	(180, '2026-11-18 13:42:56.135973-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(181, '2026-11-18 13:42:55.627119-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-18T13:41:36.538837-05:00"}', '{"ultimo_acceso": "2026-11-18T13:42:56.135971-05:00"}', NULL, '::1'),
	(182, '2026-11-18 13:42:56.56253-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-18T18:41:36.538837+00:00"}', '{"UltimoAcceso": "2026-11-18T18:42:56.1359718+00:00"}', NULL, '::1'),
	(183, '2026-11-18 13:43:49.211437-05', 7, 'INICIO_SESION', 'seguridad', 'usuario', '7', 6, NULL, NULL, NULL, '::1'),
	(184, '2026-11-18 13:43:48.704856-05', 7, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"ultimo_acceso": "2026-11-18T09:38:41.605738-05:00"}', '{"ultimo_acceso": "2026-11-18T13:43:49.211435-05:00"}', NULL, '::1'),
	(185, '2026-11-18 13:43:49.48994-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"UltimoAcceso": "2026-11-18T14:38:41.605738+00:00"}', '{"UltimoAcceso": "2026-11-18T18:43:49.2114358+00:00"}', NULL, '::1'),
	(186, '2026-11-18 13:45:09.651013-05', 7, 'CREAR', 'turnos', 'solicitud_cambio_turno', '1', NULL, NULL, '{"motivo": "Necesito hacer unas diligencias en horarios de la mañana", "resuelto_en": null, "resuelto_por": null, "solicitado_en": "2026-11-18T13:45:09.633249-05:00", "motivo_rechazo": null, "id_solicitud_cambio": 1, "cod_estado_solicitud": "PENDIENTE", "id_asignacion_companero": 8, "id_asignacion_solicitante": 4}', NULL, '::1'),
	(187, '2026-11-18 13:45:10.40896-05', 7, 'CREAR', 'turnos', 'solicitud_cambio_turno', '1', NULL, NULL, '{"Motivo": "Necesito hacer unas diligencias en horarios de la mañana", "SolicitadoEn": "2026-11-18T18:45:09.6332496+00:00", "IdSolicitudCambio": -9223372036854774807, "CodEstadoSolicitud": "PENDIENTE", "IdAsignacionCompanero": 8, "IdAsignacionSolicitante": 4}', NULL, '::1'),
	(188, '2026-11-18 13:46:41.974546-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(189, '2026-11-18 13:46:41.468126-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-18T13:42:56.135971-05:00"}', '{"ultimo_acceso": "2026-11-18T13:46:41.974545-05:00"}', NULL, '::1'),
	(190, '2026-11-18 13:46:42.228243-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-18T18:42:56.135971+00:00"}', '{"UltimoAcceso": "2026-11-18T18:46:41.9745451+00:00"}', NULL, '::1'),
	(199, '2026-11-18 13:47:18.970903-05', 6, 'MODIFICAR', 'turnos', 'solicitud_cambio_turno', '1', NULL, '{"resuelto_en": null, "resuelto_por": null, "motivo_rechazo": null, "cod_estado_solicitud": "PENDIENTE"}', '{"resuelto_en": "2026-11-18T13:47:19.341859-05:00", "resuelto_por": 6, "motivo_rechazo": "El cambio de turno no se puede aprobar para el empleado #6 el 2026-11-19: Entre este turno y el turno del 2026-11-20 hay 8 h de descanso; el mínimo es 12 h. No se aplicó ningún cambio.", "cod_estado_solicitud": "RECHAZADA"}', NULL, '::1'),
	(200, '2026-11-18 13:47:19.790494-05', 6, 'MODIFICAR', 'turnos', 'solicitud_cambio_turno', '1', NULL, '{"ResueltoEn": null, "ResueltoPor": null, "MotivoRechazo": null, "CodEstadoSolicitud": "PENDIENTE"}', '{"ResueltoEn": "2026-11-18T18:47:19.3418594+00:00", "ResueltoPor": 6, "MotivoRechazo": "El cambio de turno no se puede aprobar para el empleado #6 el 2026-11-19: Entre este turno y el turno del 2026-11-20 hay 8 h de descanso; el mínimo es 12 h. No se aplicó ningún cambio.", "CodEstadoSolicitud": "RECHAZADA"}', NULL, '::1'),
	(201, '2026-11-18 13:47:44.534362-05', 7, 'INICIO_SESION', 'seguridad', 'usuario', '7', 6, NULL, NULL, NULL, '::1'),
	(202, '2026-11-18 13:47:44.029169-05', 7, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"ultimo_acceso": "2026-11-18T13:43:49.211435-05:00"}', '{"ultimo_acceso": "2026-11-18T13:47:44.534361-05:00"}', NULL, '::1'),
	(203, '2026-11-18 13:47:44.824615-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"UltimoAcceso": "2026-11-18T18:43:49.211435+00:00"}', '{"UltimoAcceso": "2026-11-18T18:47:44.5343611+00:00"}', NULL, '::1'),
	(204, '2026-11-18 13:53:09.077876-05', 7, 'CREAR', 'seguridad', 'usuario_foto_perfil', '7', NULL, NULL, '{"url": "https://res.cloudinary.com/deh93p4on/image/upload/v1795027987/gestion-turnos/perfiles/usuario-7.jpg", "public_id": "gestion-turnos/perfiles/usuario-7", "id_usuario": 7, "actualizado_en": "2026-11-18T13:53:09.348083-05:00"}', NULL, '::1'),
	(205, '2026-11-18 13:53:09.975357-05', 7, 'CREAR', 'seguridad', 'usuario_foto_perfil', '7', NULL, NULL, '{"Url": "https://res.cloudinary.com/deh93p4on/image/upload/v1795027987/gestion-turnos/perfiles/usuario-7.jpg", "PublicId": "gestion-turnos/perfiles/usuario-7", "IdUsuario": 7, "ActualizadoEn": "2026-11-18T18:53:09.3480834+00:00"}', NULL, '::1'),
	(206, '2026-11-18 13:53:33.91238-05', 7, 'INICIO_SESION', 'seguridad', 'usuario', '7', 6, NULL, NULL, NULL, '::1'),
	(207, '2026-11-18 13:53:33.413904-05', 7, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"ultimo_acceso": "2026-11-18T13:47:44.534361-05:00"}', '{"ultimo_acceso": "2026-11-18T13:53:33.912296-05:00"}', NULL, '::1'),
	(208, '2026-11-18 13:53:34.219618-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"UltimoAcceso": "2026-11-18T18:47:44.534361+00:00"}', '{"UltimoAcceso": "2026-11-18T18:53:33.9122962+00:00"}', NULL, '::1'),
	(209, '2026-11-18 13:59:24.907267-05', 7, 'MODIFICAR', 'seguridad', 'usuario_foto_perfil', '7', NULL, '{"url": "https://res.cloudinary.com/deh93p4on/image/upload/v1795027987/gestion-turnos/perfiles/usuario-7.jpg", "actualizado_en": "2026-11-18T13:53:09.348083-05:00"}', '{"url": "https://res.cloudinary.com/deh93p4on/image/upload/v1795028363/gestion-turnos/perfiles/usuario-7.png", "actualizado_en": "2026-11-18T13:59:25.228076-05:00"}', NULL, '::1'),
	(210, '2026-11-18 13:59:25.67272-05', 7, 'MODIFICAR', 'seguridad', 'usuario_foto_perfil', '7', NULL, '{"Url": "https://res.cloudinary.com/deh93p4on/image/upload/v1795027987/gestion-turnos/perfiles/usuario-7.jpg", "ActualizadoEn": "2026-11-18T18:53:09.348083+00:00"}', '{"Url": "https://res.cloudinary.com/deh93p4on/image/upload/v1795028363/gestion-turnos/perfiles/usuario-7.png", "ActualizadoEn": "2026-11-18T18:59:25.2280762+00:00"}', NULL, '::1'),
	(211, '2026-11-18 14:00:36.970551-05', 7, 'MODIFICAR', 'seguridad', 'usuario', '7', 6, NULL, NULL, 'Cambio de contraseña', '::1'),
	(212, '2026-11-18 15:51:45.546887-05', 7, 'INICIO_SESION', 'seguridad', 'usuario', '7', 6, NULL, NULL, NULL, '::1'),
	(213, '2026-11-18 15:51:45.164566-05', 7, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"ultimo_acceso": "2026-11-18T13:53:33.912296-05:00"}', '{"ultimo_acceso": "2026-11-18T15:51:45.546884-05:00"}', NULL, '::1'),
	(214, '2026-11-18 15:51:45.825597-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"UltimoAcceso": "2026-11-18T18:53:33.912296+00:00"}', '{"UltimoAcceso": "2026-11-18T20:51:45.5468845+00:00"}', NULL, '::1'),
	(215, '2026-11-18 16:32:57.473509-05', 7, 'INICIO_SESION', 'seguridad', 'usuario', '7', 6, NULL, NULL, NULL, '::1'),
	(216, '2026-11-18 16:32:57.12962-05', 7, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"ultimo_acceso": "2026-11-18T15:51:45.546884-05:00"}', '{"ultimo_acceso": "2026-11-18T16:32:57.473299-05:00"}', NULL, '::1'),
	(217, '2026-11-18 16:32:57.980843-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"UltimoAcceso": "2026-11-18T20:51:45.546884+00:00"}', '{"UltimoAcceso": "2026-11-18T21:32:57.4732993+00:00"}', NULL, '::1'),
	(218, '2026-11-18 17:12:23.971139-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '181.143.2.194'),
	(219, '2026-11-18 17:12:23.926318-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-18T13:46:41.974545-05:00"}', '{"ultimo_acceso": "2026-11-18T17:12:23.971037-05:00"}', NULL, '181.143.2.194'),
	(220, '2026-11-18 17:12:24.610331-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-18T18:46:41.974545+00:00"}', '{"UltimoAcceso": "2026-11-18T22:12:23.9710371+00:00"}', NULL, '181.143.2.194'),
	(221, '2026-11-18 17:13:26.290812-05', 7, 'INICIO_SESION', 'seguridad', 'usuario', '7', 6, NULL, NULL, NULL, '181.143.2.194'),
	(222, '2026-11-18 17:13:26.24612-05', 7, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"ultimo_acceso": "2026-11-18T16:32:57.473299-05:00"}', '{"ultimo_acceso": "2026-11-18T17:13:26.290797-05:00"}', NULL, '181.143.2.194'),
	(223, '2026-11-18 17:13:26.600918-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"UltimoAcceso": "2026-11-18T21:32:57.473299+00:00"}', '{"UltimoAcceso": "2026-11-18T22:13:26.2907976+00:00"}', NULL, '181.143.2.194'),
	(224, '2026-11-18 17:14:34.35178-05', 7, 'MODIFICAR', 'asistencia', 'registro', '1', 6, '{"hora_salida": null}', '{"hora_salida": "2026-11-18T17:14:34.202654-05:00"}', NULL, '181.143.2.194'),
	(225, '2026-11-18 17:14:35.101169-05', 7, 'MODIFICAR', 'asistencia', 'registro', '1', 6, '{"HoraSalida": null}', '{"HoraSalida": "2026-11-18T22:14:34.2026547+00:00"}', NULL, '181.143.2.194'),
	(226, '2026-11-18 17:14:36.321496-05', 7, 'CREAR', 'asistencia', 'detalle_horas', '1,ORDINARIA', NULL, NULL, '{"Minutos": 627, "IdRegistro": 1, "CodCategoriaHora": "ORDINARIA"}', NULL, '181.143.2.194'),
	(227, '2026-11-18 17:15:05.364391-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '181.143.2.194'),
	(228, '2026-11-18 17:15:05.320819-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-18T17:12:23.971037-05:00"}', '{"ultimo_acceso": "2026-11-18T17:15:05.364386-05:00"}', NULL, '181.143.2.194');
INSERT INTO auditoria.bitacora_negocio OVERRIDING SYSTEM VALUE VALUES
	(229, '2026-11-18 17:15:05.658166-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-18T22:12:23.971037+00:00"}', '{"UltimoAcceso": "2026-11-18T22:15:05.3643862+00:00"}', NULL, '181.143.2.194'),
	(230, '2026-11-18 17:16:43.186909-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '181.51.32.131'),
	(231, '2026-11-18 17:16:43.142307-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-18T17:15:05.364386-05:00"}', '{"ultimo_acceso": "2026-11-18T17:16:43.186904-05:00"}', NULL, '181.51.32.131'),
	(232, '2026-11-18 17:16:43.484473-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-18T22:15:05.364386+00:00"}', '{"UltimoAcceso": "2026-11-18T22:16:43.1869045+00:00"}', NULL, '181.51.32.131'),
	(233, '2026-11-18 17:17:20.655371-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '181.143.2.194'),
	(234, '2026-11-18 17:17:20.610269-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-18T17:16:43.186904-05:00"}', '{"ultimo_acceso": "2026-11-18T17:17:20.655354-05:00"}', NULL, '181.143.2.194'),
	(235, '2026-11-18 17:17:20.942194-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-18T22:16:43.186904+00:00"}', '{"UltimoAcceso": "2026-11-18T22:17:20.6553547+00:00"}', NULL, '181.143.2.194'),
	(236, '2026-11-18 17:18:30.441723-05', 7, 'INICIO_SESION', 'seguridad', 'usuario', '7', 6, NULL, NULL, NULL, '::1'),
	(237, '2026-11-18 17:18:30.149342-05', 7, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"ultimo_acceso": "2026-11-18T17:13:26.290797-05:00"}', '{"ultimo_acceso": "2026-11-18T17:18:30.441491-05:00"}', NULL, '::1'),
	(238, '2026-11-18 17:18:30.849693-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '7', NULL, '{"UltimoAcceso": "2026-11-18T22:13:26.290797+00:00"}', '{"UltimoAcceso": "2026-11-18T22:18:30.4414918+00:00"}', NULL, '::1'),
	(239, '2026-11-18 17:19:06.827092-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(240, '2026-11-18 17:19:06.572601-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-18T17:17:20.655354-05:00"}', '{"ultimo_acceso": "2026-11-18T17:19:06.82666-05:00"}', NULL, '::1'),
	(241, '2026-11-18 17:19:07.848371-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-18T22:17:20.655354+00:00"}', '{"UltimoAcceso": "2026-11-18T22:19:06.8266606+00:00"}', NULL, '::1'),
	(242, '2026-11-18 17:24:11.279754-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '::1'),
	(243, '2026-11-18 17:24:11.211566-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-18T17:19:06.82666-05:00"}', '{"ultimo_acceso": "2026-11-18T17:24:11.279256-05:00"}', NULL, '::1'),
	(244, '2026-11-18 17:24:11.701083-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-18T22:19:06.82666+00:00"}', '{"UltimoAcceso": "2026-11-18T22:24:11.2792565+00:00"}', NULL, '::1'),
	(245, '2026-11-18 17:30:21.9121-05', NULL, 'CREAR', 'seguridad', 'usuario', '9', NULL, NULL, '{"correo": "laura.gomez.pruebas@gestionturnos.local", "id_rol": 2, "creado_en": "2026-11-18T17:30:21.9121-05:00", "id_usuario": 9, "ultimo_acceso": null, "cod_estado_usuario": "ACTIVO"}', NULL, NULL),
	(246, '2026-11-18 17:30:21.9121-05', NULL, 'CREAR', 'organizacion', 'empleado', '8', 8, NULL, '{"id_area": 5, "nombres": "Laura Valentina", "id_cargo": 5, "telefono": "3157654321", "apellidos": "Gómez Rincón", "creado_en": "2026-11-18T17:30:21.9121-05:00", "direccion": "Carrera 15 # 82-40, Bogotá", "id_usuario": 9, "id_empleado": 8, "fecha_retiro": null, "fecha_ingreso": "2025-03-03", "correo_contacto": "laura.gomez.pruebas@gestionturnos.local", "numero_documento": "1019876543", "cod_tipo_contrato": "INDEFINIDO", "cod_tipo_documento": "CC", "cod_estado_empleado": "ACTIVO"}', NULL, NULL),
	(247, '2026-11-18 17:30:21.9121-05', NULL, 'CREAR', 'novedades', 'periodo_vacacional', '1', 8, NULL, '{"fecha_fin": "2026-03-02", "id_empleado": 8, "fecha_inicio": "2025-03-03", "dias_otorgados": 15.00, "id_periodo_vacacional": 1}', NULL, NULL),
	(248, '2026-11-18 17:33:30.082708-05', 6, 'INICIO_SESION', 'seguridad', 'usuario', '6', 5, NULL, NULL, NULL, '181.143.2.194'),
	(249, '2026-11-18 17:33:30.028177-05', 6, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"ultimo_acceso": "2026-11-18T17:24:11.279256-05:00"}', '{"ultimo_acceso": "2026-11-18T17:33:30.082633-05:00"}', NULL, '181.143.2.194'),
	(250, '2026-11-18 17:33:30.699217-05', NULL, 'MODIFICAR', 'seguridad', 'usuario', '6', NULL, '{"UltimoAcceso": "2026-11-18T22:24:11.279256+00:00"}', '{"UltimoAcceso": "2026-11-18T22:33:30.0826336+00:00"}', NULL, '181.143.2.194');


--
-- Data for Name: novedad; Type: TABLE DATA; Schema: novedades; Owner: -
--

INSERT INTO novedades.novedad OVERRIDING SYSTEM VALUE VALUES
	(1, 6, 'INCAPACIDAD', '2026-11-16 06:00:00-05', '2026-11-17 14:00:00-05', true, true, 'APROBADA', 'Tuve una crisis de Migraña y me enviaron incapacidad medica de 2 días', 'https://www.puertocarreno-vichada.gov.co/Transparencia/finanzas_municipales/FACTURA%20001%20-%2036274845%20-%202026.pdf', 7, '2026-11-18 08:03:02.311126-05', 6, '2026-11-18 08:04:21.125806-05', NULL);


--
-- Data for Name: periodo_vacacional; Type: TABLE DATA; Schema: novedades; Owner: -
--

INSERT INTO novedades.periodo_vacacional OVERRIDING SYSTEM VALUE VALUES
	(1, 8, '2025-03-03', '2026-03-02', 15.00);


--
-- Data for Name: solicitud_vacaciones; Type: TABLE DATA; Schema: novedades; Owner: -
--



--
-- Data for Name: area; Type: TABLE DATA; Schema: organizacion; Owner: -
--

INSERT INTO organizacion.area OVERRIDING SYSTEM VALUE VALUES
	(5, 5, 'Administración', true);


--
-- Data for Name: cargo; Type: TABLE DATA; Schema: organizacion; Owner: -
--

INSERT INTO organizacion.cargo OVERRIDING SYSTEM VALUE VALUES
	(5, 'Administrador del sistema', true);


--
-- Data for Name: empleado; Type: TABLE DATA; Schema: organizacion; Owner: -
--

INSERT INTO organizacion.empleado OVERRIDING SYSTEM VALUE VALUES
	(5, 6, 'CC', '1000000001', 'Andrés Julián', 'Muñoz Peña', 'admin@gestionturnos.local', '3001234567', 'Calle 10 # 5-23, Bogotá', 5, 5, 'INDEFINIDO', '2026-01-01', NULL, 'ACTIVO', '2026-11-17 11:44:53.200755-05'),
	(6, 7, 'CC', '1033487560', 'Samuel', 'Díaz Pérez', 'samueldiazp.dev@gmail.com', '3106653490', NULL, 5, 5, 'INDEFINIDO', '2026-11-17', NULL, 'ACTIVO', '2026-11-17 17:02:52.390491-05'),
	(7, 8, 'CC', '1022483250', 'Juan Sebastián', 'Montaño Pérez', 'samueldiazperez4c@gmail.com', '3106653493', NULL, 5, 5, 'INDEFINIDO', '2026-11-18', NULL, 'ACTIVO', '2026-11-18 11:25:42.446835-05'),
	(8, 9, 'CC', '1019876543', 'Laura Valentina', 'Gómez Rincón', 'laura.gomez.pruebas@gestionturnos.local', '3157654321', 'Carrera 15 # 82-40, Bogotá', 5, 5, 'INDEFINIDO', '2025-03-03', NULL, 'ACTIVO', '2026-11-18 17:30:21.9121-05');


--
-- Data for Name: sucursal; Type: TABLE DATA; Schema: organizacion; Owner: -
--

INSERT INTO organizacion.sucursal OVERRIDING SYSTEM VALUE VALUES
	(5, 'Sede Principal', 'Calle 10 # 5-23', 'Bogotá', true);


--
-- Data for Name: categoria_hora; Type: TABLE DATA; Schema: parametrizacion; Owner: -
--

INSERT INTO parametrizacion.categoria_hora VALUES
	('ORDINARIA', 'Hora ordinaria', 1.00, true),
	('RECARGO_NOCTURNO', 'Recargo nocturno', 1.35, true),
	('EXTRA_DIURNA', 'Hora extra diurna', 1.25, true),
	('EXTRA_NOCTURNA', 'Hora extra nocturna', 1.75, true),
	('DOMINICAL_FESTIVO', 'Dominical o festivo', 1.75, true);


--
-- Data for Name: dia_festivo; Type: TABLE DATA; Schema: parametrizacion; Owner: -
--



--
-- Data for Name: estado_asignacion; Type: TABLE DATA; Schema: parametrizacion; Owner: -
--

INSERT INTO parametrizacion.estado_asignacion VALUES
	('PROGRAMADA', 'Programada'),
	('CONFIRMADA', 'Confirmada / publicada'),
	('ANULADA', 'Anulada');


--
-- Data for Name: estado_empleado; Type: TABLE DATA; Schema: parametrizacion; Owner: -
--

INSERT INTO parametrizacion.estado_empleado VALUES
	('ACTIVO', 'Activo'),
	('INACTIVO', 'Inactivo'),
	('SUSPENDIDO', 'Suspendido');


--
-- Data for Name: estado_solicitud; Type: TABLE DATA; Schema: parametrizacion; Owner: -
--

INSERT INTO parametrizacion.estado_solicitud VALUES
	('PENDIENTE', 'Pendiente', false),
	('APROBADA', 'Aprobada', true),
	('RECHAZADA', 'Rechazada', true),
	('CANCELADA', 'Cancelada', true);


--
-- Data for Name: estado_usuario; Type: TABLE DATA; Schema: parametrizacion; Owner: -
--

INSERT INTO parametrizacion.estado_usuario VALUES
	('ACTIVO', 'Activo'),
	('INACTIVO', 'Inactivo'),
	('BLOQUEADO', 'Bloqueado');


--
-- Data for Name: fuente_marcacion; Type: TABLE DATA; Schema: parametrizacion; Owner: -
--

INSERT INTO parametrizacion.fuente_marcacion VALUES
	('WEB', 'Portal web'),
	('MOVIL', 'Aplicación móvil'),
	('ADMIN', 'Registro manual del administrador'),
	('BIOMETRICO', 'Reloj biométrico');


--
-- Data for Name: parametro; Type: TABLE DATA; Schema: parametrizacion; Owner: -
--

INSERT INTO parametrizacion.parametro VALUES
	('JORNADA_MAX_HORAS_DIA', 'Horas máximas por día', NULL, 'DECIMAL', true),
	('JORNADA_MAX_HORAS_SEMANA', 'Horas máximas por semana', NULL, 'DECIMAL', true),
	('DESCANSO_MIN_ENTRE_TURNOS', 'Horas mínimas de descanso entre turnos', NULL, 'DECIMAL', true),
	('HORA_INICIO_NOCTURNA', 'Hora de inicio de la jornada nocturna', NULL, 'HORA', true),
	('HORA_FIN_NOCTURNA', 'Hora de fin de la jornada nocturna', NULL, 'HORA', true),
	('TOLERANCIA_ENTRADA_MIN', 'Minutos de tolerancia en la entrada', NULL, 'ENTERO', true),
	('VACACIONES_DIAS_POR_ANIO', 'Días hábiles de vacaciones por año', NULL, 'DECIMAL', true),
	('CONFLICTO_BLOQUEA', 'Un conflicto bloquea la asignación (si no, solo advierte)', NULL, 'BOOLEANO', true);


--
-- Data for Name: parametro_valor; Type: TABLE DATA; Schema: parametrizacion; Owner: -
--

INSERT INTO parametrizacion.parametro_valor OVERRIDING SYSTEM VALUE VALUES
	(1, 'JORNADA_MAX_HORAS_DIA', '10', '2026-01-01', NULL),
	(2, 'JORNADA_MAX_HORAS_SEMANA', '44', '2026-01-01', NULL),
	(3, 'DESCANSO_MIN_ENTRE_TURNOS', '12', '2026-01-01', NULL),
	(4, 'HORA_INICIO_NOCTURNA', '19:00', '2026-01-01', NULL),
	(5, 'HORA_FIN_NOCTURNA', '06:00', '2026-01-01', NULL),
	(6, 'TOLERANCIA_ENTRADA_MIN', '5', '2026-01-01', NULL),
	(7, 'VACACIONES_DIAS_POR_ANIO', '15', '2026-01-01', NULL),
	(8, 'CONFLICTO_BLOQUEA', 'true', '2026-01-01', NULL);


--
-- Data for Name: tipo_accion; Type: TABLE DATA; Schema: parametrizacion; Owner: -
--

INSERT INTO parametrizacion.tipo_accion VALUES
	('CREAR', 'Creación'),
	('MODIFICAR', 'Modificación'),
	('ELIMINAR', 'Eliminación'),
	('APROBAR', 'Aprobación'),
	('RECHAZAR', 'Rechazo'),
	('INICIO_SESION', 'Inicio de sesión');


--
-- Data for Name: tipo_contrato; Type: TABLE DATA; Schema: parametrizacion; Owner: -
--

INSERT INTO parametrizacion.tipo_contrato VALUES
	('INDEFINIDO', 'Término indefinido', true),
	('FIJO', 'Término fijo', true),
	('OBRA_LABOR', 'Obra o labor', true),
	('APRENDIZAJE', 'Aprendizaje', true),
	('PRESTACION', 'Prestación de servicios', true);


--
-- Data for Name: tipo_documento; Type: TABLE DATA; Schema: parametrizacion; Owner: -
--

INSERT INTO parametrizacion.tipo_documento VALUES
	('CC', 'Cédula de ciudadanía', true),
	('CE', 'Cédula de extranjería', true),
	('PA', 'Pasaporte', true),
	('PPT', 'Permiso por protección temporal', true);


--
-- Data for Name: tipo_novedad; Type: TABLE DATA; Schema: parametrizacion; Owner: -
--

INSERT INTO parametrizacion.tipo_novedad VALUES
	('INCAPACIDAD', 'Incapacidad médica', true, true, false, true, true),
	('CALAMIDAD', 'Calamidad doméstica', true, true, true, false, true),
	('PERMISO_PERSONAL', 'Permiso personal', false, true, true, false, true),
	('AUSENCIA_JUST', 'Ausencia justificada', true, true, false, true, true),
	('AUSENCIA_INJUST', 'Ausencia injustificada', false, true, false, false, true);


--
-- Data for Name: DataProtectionKeys; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public."DataProtectionKeys" VALUES
	(1, 'key-0e239ab8-5ac1-49f1-9e52-f154766fe9be', '<key id="0e239ab8-5ac1-49f1-9e52-f154766fe9be" version="1"><creationDate>2026-11-16T23:55:09.8486967Z</creationDate><activationDate>2026-11-16T23:55:09.8486967Z</activationDate><expirationDate>2027-02-14T23:55:09.8486967Z</expirationDate><descriptor deserializerType="Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel.AuthenticatedEncryptorDescriptorDeserializer, Microsoft.AspNetCore.DataProtection, Version=10.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60"><descriptor><encryption algorithm="AES_256_CBC" /><validation algorithm="HMACSHA256" /><masterKey p4:requiresEncryption="true" xmlns:p4="http://schemas.asp.net/2015/03/dataProtection"><!-- Warning: the key below is in an unencrypted form. --><value>sq4K+K/T9nbNeRqkReKNs7y1YQRyjcaSkF3EXU54GlMcs+q3ADkRXk9RYm3hrio0MEt1oKxi3YTmoVYl81+WwQ==</value></masterKey></descriptor></descriptor></key>');


--
-- Data for Name: permiso; Type: TABLE DATA; Schema: seguridad; Owner: -
--

INSERT INTO seguridad.permiso OVERRIDING SYSTEM VALUE VALUES
	(1, 'usuarios.gestionar', 'Crear, activar y desactivar usuarios; administrar roles y permisos'),
	(2, 'registros.aprobar', 'Aprobar o rechazar solicitudes de registro'),
	(3, 'empleados.gestionar', 'Crear, actualizar, desactivar y reactivar empleados'),
	(4, 'organizacion.gestionar', 'Sucursales, áreas y cargos'),
	(5, 'turnos.configurar', 'Tipos de turno'),
	(6, 'turnos.programar', 'Asignar, modificar, anular y publicar turnos; descansos y cambios de turno'),
	(7, 'asistencia.consultar', 'Ver la asistencia de todos los empleados'),
	(8, 'asistencia.registrar_manual', 'Registrar o corregir marcaciones'),
	(9, 'vacaciones.aprobar', 'Registrar días causados y resolver solicitudes de vacaciones'),
	(10, 'novedades.aprobar', 'Resolver y registrar permisos y ausencias'),
	(11, 'parametros.editar', 'Parámetros de negocio y catálogos'),
	(12, 'reportes.consultar', 'Calendario general y reportes'),
	(13, 'auditoria.consultar', 'Consultar la bitácora de auditoría');


--
-- Data for Name: rol; Type: TABLE DATA; Schema: seguridad; Owner: -
--

INSERT INTO seguridad.rol OVERRIDING SYSTEM VALUE VALUES
	(2, 'Empleado', 'Consulta y solicitudes sobre su propia información', true),
	(1, 'Administrador', 'Gestión general de la plataforma', true);


--
-- Data for Name: rol_permiso; Type: TABLE DATA; Schema: seguridad; Owner: -
--

INSERT INTO seguridad.rol_permiso VALUES
	(1, 1),
	(1, 2),
	(1, 3),
	(1, 4),
	(1, 5),
	(1, 6),
	(1, 7),
	(1, 8),
	(1, 9),
	(1, 10),
	(1, 11),
	(1, 12),
	(1, 13);


--
-- Data for Name: solicitud_registro; Type: TABLE DATA; Schema: seguridad; Owner: -
--

INSERT INTO seguridad.solicitud_registro OVERRIDING SYSTEM VALUE VALUES
	(1, 'CC', '1033487560', 'Samuel', 'Díaz Pérez', 'samueldiazp.dev@gmail.com', '3106653490', '$2a$11$kWZ.CIaDHi8roSghqWErvOva.FFsfhFdCvOijVOEbSgoGDL9nNZSW', 'APROBADA', '2026-11-17 16:26:10.523968-05', 6, '2026-11-17 17:02:52.982145-05', NULL, 7),
	(2, 'CC', '1022483250', 'Juan Sebastián', 'Montaño Pérez', 'samueldiazperez4c@gmail.com', '3106653493', '$2a$11$u2bVHH1vwk0dIBvPg2dKjuw5XdWhETJpsvEYecA0JX9D.LQoOTYVK', 'APROBADA', '2026-11-18 11:25:02.218293-05', 6, '2026-11-18 11:25:42.854138-05', NULL, 8);


--
-- Data for Name: usuario; Type: TABLE DATA; Schema: seguridad; Owner: -
--

INSERT INTO seguridad.usuario OVERRIDING SYSTEM VALUE VALUES
	(1, 'admin@tuempresa.com', '<hash bcrypt>', 1, 'ACTIVO', NULL, '2026-11-16 18:40:06.797755-05'),
	(9, 'laura.gomez.pruebas@gestionturnos.local', '$2a$11$TxCtURMz/p8mEBXd9xtzS.lRwkD3RgwQHG4.nPo2bHB2RSmsecCE6', 2, 'ACTIVO', NULL, '2026-11-18 17:30:21.9121-05'),
	(6, 'admin@gestionturnos.local', '$2a$11$45jexQwq2p47LttgVLoz7.VFhvy6x37ivRkeXmTjwg33nVQWT6uUi', 1, 'ACTIVO', '2026-11-18 17:33:30.082633-05', '2026-11-17 11:44:53.200755-05'),
	(8, 'samueldiazperez4c@gmail.com', '$2a$11$u2bVHH1vwk0dIBvPg2dKjuw5XdWhETJpsvEYecA0JX9D.LQoOTYVK', 2, 'ACTIVO', NULL, '2026-11-18 11:25:42.015438-05'),
	(7, 'samueldiazp.dev@gmail.com', '$2a$11$cu.FLsr3Ify9EkpIrV3sC.ARfHVaw1BiaZ2fnQoPxoxgSThN6uuy.', 2, 'ACTIVO', '2026-11-18 17:18:30.441491-05', '2026-11-17 17:02:51.524952-05');


--
-- Data for Name: usuario_foto_perfil; Type: TABLE DATA; Schema: seguridad; Owner: -
--

INSERT INTO seguridad.usuario_foto_perfil VALUES
	(7, 'https://res.cloudinary.com/deh93p4on/image/upload/v1795028363/gestion-turnos/perfiles/usuario-7.png', 'gestion-turnos/perfiles/usuario-7', '2026-11-18 13:59:25.228076-05');


--
-- Data for Name: asignacion; Type: TABLE DATA; Schema: turnos; Owner: -
--

INSERT INTO turnos.asignacion OVERRIDING SYSTEM VALUE VALUES
	(1, 6, 1, '2026-11-16', '["2026-11-16 06:00:00-05","2026-11-16 14:00:00-05")', 'CONFIRMADA', NULL, 6, '2026-11-18 07:42:41.50702-05'),
	(2, 6, 1, '2026-11-17', '["2026-11-17 06:00:00-05","2026-11-17 14:00:00-05")', 'CONFIRMADA', NULL, 6, '2026-11-18 07:42:42.901471-05'),
	(3, 6, 1, '2026-11-18', '["2026-11-18 06:00:00-05","2026-11-18 14:00:00-05")', 'CONFIRMADA', NULL, 6, '2026-11-18 07:42:44.102396-05'),
	(4, 6, 1, '2026-11-19', '["2026-11-19 06:00:00-05","2026-11-19 14:00:00-05")', 'CONFIRMADA', NULL, 6, '2026-11-18 07:42:45.36969-05'),
	(5, 6, 1, '2026-11-20', '["2026-11-20 06:00:00-05","2026-11-20 14:00:00-05")', 'CONFIRMADA', NULL, 6, '2026-11-18 07:42:46.574916-05'),
	(6, 5, 1, '2026-11-16', '["2026-11-16 06:00:00-05","2026-11-16 14:00:00-05")', 'ANULADA', 'Prueba de anulación', 6, '2026-11-18 10:31:17.13985-05'),
	(7, 7, 2, '2026-11-18', '["2026-11-18 14:00:00-05","2026-11-18 22:00:00-05")', 'CONFIRMADA', 'Debe de recibirle el turno a Samuel Díaz', 6, '2026-11-18 11:41:21.674395-05'),
	(8, 7, 2, '2026-11-19', '["2026-11-19 14:00:00-05","2026-11-19 22:00:00-05")', 'CONFIRMADA', 'Debe de recibirle el turno a Samuel Díaz', 6, '2026-11-18 11:41:22.909822-05'),
	(9, 7, 2, '2026-11-20', '["2026-11-20 14:00:00-05","2026-11-20 22:00:00-05")', 'CONFIRMADA', 'Debe de recibirle el turno a Samuel Díaz', 6, '2026-11-18 11:41:24.371789-05');


--
-- Data for Name: dia_descanso; Type: TABLE DATA; Schema: turnos; Owner: -
--



--
-- Data for Name: solicitud_cambio_turno; Type: TABLE DATA; Schema: turnos; Owner: -
--

INSERT INTO turnos.solicitud_cambio_turno OVERRIDING SYSTEM VALUE VALUES
	(1, 4, 8, 'Necesito hacer unas diligencias en horarios de la mañana', 'RECHAZADA', '2026-11-18 13:45:09.633249-05', 6, '2026-11-18 13:47:19.341859-05', 'El cambio de turno no se puede aprobar para el empleado #6 el 2026-11-19: Entre este turno y el turno del 2026-11-20 hay 8 h de descanso; el mínimo es 12 h. No se aplicó ningún cambio.');


--
-- Data for Name: turno; Type: TABLE DATA; Schema: turnos; Owner: -
--

INSERT INTO turnos.turno OVERRIDING SYSTEM VALUE VALUES
	(1, 'Mañana', '06:00:00', '14:00:00', 30, DEFAULT, DEFAULT, '#F5B041', true),
	(2, 'Tarde', '14:00:00', '22:00:00', 30, DEFAULT, DEFAULT, '#5DADE2', true),
	(3, 'Noche', '22:00:00', '06:00:00', 30, DEFAULT, DEFAULT, '#34495E', true),
	(4, 'Fin de semana', '08:00:00', '18:00:00', 60, DEFAULT, DEFAULT, '#58D68D', true);


--
-- Name: registro_id_registro_seq; Type: SEQUENCE SET; Schema: asistencia; Owner: -
--

SELECT pg_catalog.setval('asistencia.registro_id_registro_seq', 1, true);


--
-- Name: bitacora_negocio_id_bitacora_seq; Type: SEQUENCE SET; Schema: auditoria; Owner: -
--

SELECT pg_catalog.setval('auditoria.bitacora_negocio_id_bitacora_seq', 250, true);


--
-- Name: novedad_id_novedad_seq; Type: SEQUENCE SET; Schema: novedades; Owner: -
--

SELECT pg_catalog.setval('novedades.novedad_id_novedad_seq', 1, true);


--
-- Name: periodo_vacacional_id_periodo_vacacional_seq; Type: SEQUENCE SET; Schema: novedades; Owner: -
--

SELECT pg_catalog.setval('novedades.periodo_vacacional_id_periodo_vacacional_seq', 1, true);


--
-- Name: solicitud_vacaciones_id_solicitud_vacaciones_seq; Type: SEQUENCE SET; Schema: novedades; Owner: -
--

SELECT pg_catalog.setval('novedades.solicitud_vacaciones_id_solicitud_vacaciones_seq', 1, false);


--
-- Name: area_id_area_seq; Type: SEQUENCE SET; Schema: organizacion; Owner: -
--

SELECT pg_catalog.setval('organizacion.area_id_area_seq', 5, true);


--
-- Name: cargo_id_cargo_seq; Type: SEQUENCE SET; Schema: organizacion; Owner: -
--

SELECT pg_catalog.setval('organizacion.cargo_id_cargo_seq', 5, true);


--
-- Name: empleado_id_empleado_seq; Type: SEQUENCE SET; Schema: organizacion; Owner: -
--

SELECT pg_catalog.setval('organizacion.empleado_id_empleado_seq', 8, true);


--
-- Name: sucursal_id_sucursal_seq; Type: SEQUENCE SET; Schema: organizacion; Owner: -
--

SELECT pg_catalog.setval('organizacion.sucursal_id_sucursal_seq', 5, true);


--
-- Name: parametro_valor_id_parametro_valor_seq; Type: SEQUENCE SET; Schema: parametrizacion; Owner: -
--

SELECT pg_catalog.setval('parametrizacion.parametro_valor_id_parametro_valor_seq', 8, true);


--
-- Name: DataProtectionKeys_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public."DataProtectionKeys_Id_seq"', 1, true);


--
-- Name: permiso_id_permiso_seq; Type: SEQUENCE SET; Schema: seguridad; Owner: -
--

SELECT pg_catalog.setval('seguridad.permiso_id_permiso_seq', 78, true);


--
-- Name: rol_id_rol_seq; Type: SEQUENCE SET; Schema: seguridad; Owner: -
--

SELECT pg_catalog.setval('seguridad.rol_id_rol_seq', 7, true);


--
-- Name: sesion_dispositivo_id_sesion_seq; Type: SEQUENCE SET; Schema: seguridad; Owner: -
--

SELECT pg_catalog.setval('seguridad.sesion_dispositivo_id_sesion_seq', 12, true);


--
-- Name: solicitud_registro_id_solicitud_registro_seq; Type: SEQUENCE SET; Schema: seguridad; Owner: -
--

SELECT pg_catalog.setval('seguridad.solicitud_registro_id_solicitud_registro_seq', 2, true);


--
-- Name: usuario_id_usuario_seq; Type: SEQUENCE SET; Schema: seguridad; Owner: -
--

SELECT pg_catalog.setval('seguridad.usuario_id_usuario_seq', 9, true);


--
-- Name: asignacion_id_asignacion_seq; Type: SEQUENCE SET; Schema: turnos; Owner: -
--

SELECT pg_catalog.setval('turnos.asignacion_id_asignacion_seq', 9, true);


--
-- Name: dia_descanso_id_dia_descanso_seq; Type: SEQUENCE SET; Schema: turnos; Owner: -
--

SELECT pg_catalog.setval('turnos.dia_descanso_id_dia_descanso_seq', 1, false);


--
-- Name: solicitud_cambio_turno_id_solicitud_cambio_seq; Type: SEQUENCE SET; Schema: turnos; Owner: -
--

SELECT pg_catalog.setval('turnos.solicitud_cambio_turno_id_solicitud_cambio_seq', 1, true);


--
-- Name: turno_id_turno_seq; Type: SEQUENCE SET; Schema: turnos; Owner: -
--

SELECT pg_catalog.setval('turnos.turno_id_turno_seq', 4, true);


--
-- Name: detalle_horas detalle_horas_pkey; Type: CONSTRAINT; Schema: asistencia; Owner: -
--

ALTER TABLE ONLY asistencia.detalle_horas
    ADD CONSTRAINT detalle_horas_pkey PRIMARY KEY (id_registro, cod_categoria_hora);


--
-- Name: registro registro_id_asignacion_key; Type: CONSTRAINT; Schema: asistencia; Owner: -
--

ALTER TABLE ONLY asistencia.registro
    ADD CONSTRAINT registro_id_asignacion_key UNIQUE (id_asignacion);


--
-- Name: registro registro_pkey; Type: CONSTRAINT; Schema: asistencia; Owner: -
--

ALTER TABLE ONLY asistencia.registro
    ADD CONSTRAINT registro_pkey PRIMARY KEY (id_registro);


--
-- Name: bitacora_negocio bitacora_negocio_pkey; Type: CONSTRAINT; Schema: auditoria; Owner: -
--

ALTER TABLE ONLY auditoria.bitacora_negocio
    ADD CONSTRAINT bitacora_negocio_pkey PRIMARY KEY (id_bitacora);


--
-- Name: solicitud_vacaciones ex_vacaciones_sin_cruce; Type: CONSTRAINT; Schema: novedades; Owner: -
--

ALTER TABLE ONLY novedades.solicitud_vacaciones
    ADD CONSTRAINT ex_vacaciones_sin_cruce EXCLUDE USING gist (id_empleado WITH =, daterange(fecha_inicio, fecha_fin, '[]'::text) WITH &&) WHERE (((cod_estado_solicitud)::text = ANY ((ARRAY['PENDIENTE'::character varying, 'APROBADA'::character varying])::text[])));


--
-- Name: novedad novedad_pkey; Type: CONSTRAINT; Schema: novedades; Owner: -
--

ALTER TABLE ONLY novedades.novedad
    ADD CONSTRAINT novedad_pkey PRIMARY KEY (id_novedad);


--
-- Name: periodo_vacacional periodo_vacacional_id_empleado_fecha_inicio_key; Type: CONSTRAINT; Schema: novedades; Owner: -
--

ALTER TABLE ONLY novedades.periodo_vacacional
    ADD CONSTRAINT periodo_vacacional_id_empleado_fecha_inicio_key UNIQUE (id_empleado, fecha_inicio);


--
-- Name: periodo_vacacional periodo_vacacional_pkey; Type: CONSTRAINT; Schema: novedades; Owner: -
--

ALTER TABLE ONLY novedades.periodo_vacacional
    ADD CONSTRAINT periodo_vacacional_pkey PRIMARY KEY (id_periodo_vacacional);


--
-- Name: solicitud_vacaciones solicitud_vacaciones_pkey; Type: CONSTRAINT; Schema: novedades; Owner: -
--

ALTER TABLE ONLY novedades.solicitud_vacaciones
    ADD CONSTRAINT solicitud_vacaciones_pkey PRIMARY KEY (id_solicitud_vacaciones);


--
-- Name: area area_id_sucursal_nombre_key; Type: CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.area
    ADD CONSTRAINT area_id_sucursal_nombre_key UNIQUE (id_sucursal, nombre);


--
-- Name: area area_pkey; Type: CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.area
    ADD CONSTRAINT area_pkey PRIMARY KEY (id_area);


--
-- Name: cargo cargo_nombre_key; Type: CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.cargo
    ADD CONSTRAINT cargo_nombre_key UNIQUE (nombre);


--
-- Name: cargo cargo_pkey; Type: CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.cargo
    ADD CONSTRAINT cargo_pkey PRIMARY KEY (id_cargo);


--
-- Name: empleado empleado_cod_tipo_documento_numero_documento_key; Type: CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.empleado
    ADD CONSTRAINT empleado_cod_tipo_documento_numero_documento_key UNIQUE (cod_tipo_documento, numero_documento);


--
-- Name: empleado empleado_id_usuario_key; Type: CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.empleado
    ADD CONSTRAINT empleado_id_usuario_key UNIQUE (id_usuario);


--
-- Name: empleado empleado_pkey; Type: CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.empleado
    ADD CONSTRAINT empleado_pkey PRIMARY KEY (id_empleado);


--
-- Name: sucursal sucursal_nombre_key; Type: CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.sucursal
    ADD CONSTRAINT sucursal_nombre_key UNIQUE (nombre);


--
-- Name: sucursal sucursal_pkey; Type: CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.sucursal
    ADD CONSTRAINT sucursal_pkey PRIMARY KEY (id_sucursal);


--
-- Name: categoria_hora categoria_hora_nombre_key; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.categoria_hora
    ADD CONSTRAINT categoria_hora_nombre_key UNIQUE (nombre);


--
-- Name: categoria_hora categoria_hora_pkey; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.categoria_hora
    ADD CONSTRAINT categoria_hora_pkey PRIMARY KEY (cod_categoria_hora);


--
-- Name: dia_festivo dia_festivo_pkey; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.dia_festivo
    ADD CONSTRAINT dia_festivo_pkey PRIMARY KEY (fecha);


--
-- Name: estado_asignacion estado_asignacion_nombre_key; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.estado_asignacion
    ADD CONSTRAINT estado_asignacion_nombre_key UNIQUE (nombre);


--
-- Name: estado_asignacion estado_asignacion_pkey; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.estado_asignacion
    ADD CONSTRAINT estado_asignacion_pkey PRIMARY KEY (cod_estado_asignacion);


--
-- Name: estado_empleado estado_empleado_nombre_key; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.estado_empleado
    ADD CONSTRAINT estado_empleado_nombre_key UNIQUE (nombre);


--
-- Name: estado_empleado estado_empleado_pkey; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.estado_empleado
    ADD CONSTRAINT estado_empleado_pkey PRIMARY KEY (cod_estado_empleado);


--
-- Name: estado_solicitud estado_solicitud_nombre_key; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.estado_solicitud
    ADD CONSTRAINT estado_solicitud_nombre_key UNIQUE (nombre);


--
-- Name: estado_solicitud estado_solicitud_pkey; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.estado_solicitud
    ADD CONSTRAINT estado_solicitud_pkey PRIMARY KEY (cod_estado_solicitud);


--
-- Name: estado_usuario estado_usuario_nombre_key; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.estado_usuario
    ADD CONSTRAINT estado_usuario_nombre_key UNIQUE (nombre);


--
-- Name: estado_usuario estado_usuario_pkey; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.estado_usuario
    ADD CONSTRAINT estado_usuario_pkey PRIMARY KEY (cod_estado_usuario);


--
-- Name: fuente_marcacion fuente_marcacion_nombre_key; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.fuente_marcacion
    ADD CONSTRAINT fuente_marcacion_nombre_key UNIQUE (nombre);


--
-- Name: fuente_marcacion fuente_marcacion_pkey; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.fuente_marcacion
    ADD CONSTRAINT fuente_marcacion_pkey PRIMARY KEY (cod_fuente_marcacion);


--
-- Name: parametro parametro_pkey; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.parametro
    ADD CONSTRAINT parametro_pkey PRIMARY KEY (cod_parametro);


--
-- Name: parametro_valor parametro_valor_cod_parametro_daterange_excl; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.parametro_valor
    ADD CONSTRAINT parametro_valor_cod_parametro_daterange_excl EXCLUDE USING gist (cod_parametro WITH =, daterange(vigente_desde, vigente_hasta, '[]'::text) WITH &&);


--
-- Name: parametro_valor parametro_valor_pkey; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.parametro_valor
    ADD CONSTRAINT parametro_valor_pkey PRIMARY KEY (id_parametro_valor);


--
-- Name: tipo_accion tipo_accion_nombre_key; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.tipo_accion
    ADD CONSTRAINT tipo_accion_nombre_key UNIQUE (nombre);


--
-- Name: tipo_accion tipo_accion_pkey; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.tipo_accion
    ADD CONSTRAINT tipo_accion_pkey PRIMARY KEY (cod_tipo_accion);


--
-- Name: tipo_contrato tipo_contrato_nombre_key; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.tipo_contrato
    ADD CONSTRAINT tipo_contrato_nombre_key UNIQUE (nombre);


--
-- Name: tipo_contrato tipo_contrato_pkey; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.tipo_contrato
    ADD CONSTRAINT tipo_contrato_pkey PRIMARY KEY (cod_tipo_contrato);


--
-- Name: tipo_documento tipo_documento_nombre_key; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.tipo_documento
    ADD CONSTRAINT tipo_documento_nombre_key UNIQUE (nombre);


--
-- Name: tipo_documento tipo_documento_pkey; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.tipo_documento
    ADD CONSTRAINT tipo_documento_pkey PRIMARY KEY (cod_tipo_documento);


--
-- Name: tipo_novedad tipo_novedad_nombre_key; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.tipo_novedad
    ADD CONSTRAINT tipo_novedad_nombre_key UNIQUE (nombre);


--
-- Name: tipo_novedad tipo_novedad_pkey; Type: CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.tipo_novedad
    ADD CONSTRAINT tipo_novedad_pkey PRIMARY KEY (cod_tipo_novedad);


--
-- Name: DataProtectionKeys DataProtectionKeys_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DataProtectionKeys"
    ADD CONSTRAINT "DataProtectionKeys_pkey" PRIMARY KEY ("Id");


--
-- Name: permiso permiso_codigo_key; Type: CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.permiso
    ADD CONSTRAINT permiso_codigo_key UNIQUE (codigo);


--
-- Name: permiso permiso_pkey; Type: CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.permiso
    ADD CONSTRAINT permiso_pkey PRIMARY KEY (id_permiso);


--
-- Name: rol rol_nombre_key; Type: CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.rol
    ADD CONSTRAINT rol_nombre_key UNIQUE (nombre);


--
-- Name: rol_permiso rol_permiso_pkey; Type: CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.rol_permiso
    ADD CONSTRAINT rol_permiso_pkey PRIMARY KEY (id_rol, id_permiso);


--
-- Name: rol rol_pkey; Type: CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.rol
    ADD CONSTRAINT rol_pkey PRIMARY KEY (id_rol);


--
-- Name: sesion_dispositivo sesion_dispositivo_pkey; Type: CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.sesion_dispositivo
    ADD CONSTRAINT sesion_dispositivo_pkey PRIMARY KEY (id_sesion);


--
-- Name: solicitud_registro solicitud_registro_id_usuario_creado_key; Type: CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.solicitud_registro
    ADD CONSTRAINT solicitud_registro_id_usuario_creado_key UNIQUE (id_usuario_creado);


--
-- Name: solicitud_registro solicitud_registro_pkey; Type: CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.solicitud_registro
    ADD CONSTRAINT solicitud_registro_pkey PRIMARY KEY (id_solicitud_registro);


--
-- Name: sesion_dispositivo uq_sesion_dispositivo_hash; Type: CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.sesion_dispositivo
    ADD CONSTRAINT uq_sesion_dispositivo_hash UNIQUE (hash_token);


--
-- Name: usuario_foto_perfil usuario_foto_perfil_pkey; Type: CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.usuario_foto_perfil
    ADD CONSTRAINT usuario_foto_perfil_pkey PRIMARY KEY (id_usuario);


--
-- Name: usuario usuario_pkey; Type: CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.usuario
    ADD CONSTRAINT usuario_pkey PRIMARY KEY (id_usuario);


--
-- Name: asignacion asignacion_id_asignacion_id_empleado_key; Type: CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.asignacion
    ADD CONSTRAINT asignacion_id_asignacion_id_empleado_key UNIQUE (id_asignacion, id_empleado);


--
-- Name: asignacion asignacion_pkey; Type: CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.asignacion
    ADD CONSTRAINT asignacion_pkey PRIMARY KEY (id_asignacion);


--
-- Name: dia_descanso dia_descanso_id_empleado_fecha_key; Type: CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.dia_descanso
    ADD CONSTRAINT dia_descanso_id_empleado_fecha_key UNIQUE (id_empleado, fecha);


--
-- Name: dia_descanso dia_descanso_pkey; Type: CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.dia_descanso
    ADD CONSTRAINT dia_descanso_pkey PRIMARY KEY (id_dia_descanso);


--
-- Name: asignacion ex_asignacion_sin_cruce; Type: CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.asignacion
    ADD CONSTRAINT ex_asignacion_sin_cruce EXCLUDE USING gist (id_empleado WITH =, periodo WITH &&) WHERE (((cod_estado_asignacion)::text <> 'ANULADA'::text));


--
-- Name: solicitud_cambio_turno solicitud_cambio_turno_pkey; Type: CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.solicitud_cambio_turno
    ADD CONSTRAINT solicitud_cambio_turno_pkey PRIMARY KEY (id_solicitud_cambio);


--
-- Name: turno turno_nombre_key; Type: CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.turno
    ADD CONSTRAINT turno_nombre_key UNIQUE (nombre);


--
-- Name: turno turno_pkey; Type: CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.turno
    ADD CONSTRAINT turno_pkey PRIMARY KEY (id_turno);


--
-- Name: ix_detalle_horas_cat; Type: INDEX; Schema: asistencia; Owner: -
--

CREATE INDEX ix_detalle_horas_cat ON asistencia.detalle_horas USING btree (cod_categoria_hora);


--
-- Name: ix_registro_empleado; Type: INDEX; Schema: asistencia; Owner: -
--

CREATE INDEX ix_registro_empleado ON asistencia.registro USING btree (id_empleado, hora_entrada);


--
-- Name: ix_registro_hora_entrada; Type: INDEX; Schema: asistencia; Owner: -
--

CREATE INDEX ix_registro_hora_entrada ON asistencia.registro USING btree (hora_entrada);


--
-- Name: ux_registro_abierto_por_empleado; Type: INDEX; Schema: asistencia; Owner: -
--

CREATE UNIQUE INDEX ux_registro_abierto_por_empleado ON asistencia.registro USING btree (id_empleado) WHERE ((hora_salida IS NULL) AND (NOT salida_no_registrada));


--
-- Name: ix_bitacora_accion; Type: INDEX; Schema: auditoria; Owner: -
--

CREATE INDEX ix_bitacora_accion ON auditoria.bitacora_negocio USING btree (cod_tipo_accion, fecha_hora DESC);


--
-- Name: ix_bitacora_empleado; Type: INDEX; Schema: auditoria; Owner: -
--

CREATE INDEX ix_bitacora_empleado ON auditoria.bitacora_negocio USING btree (id_empleado, fecha_hora DESC);


--
-- Name: ix_bitacora_fecha; Type: INDEX; Schema: auditoria; Owner: -
--

CREATE INDEX ix_bitacora_fecha ON auditoria.bitacora_negocio USING btree (fecha_hora DESC);


--
-- Name: ix_bitacora_registro; Type: INDEX; Schema: auditoria; Owner: -
--

CREATE INDEX ix_bitacora_registro ON auditoria.bitacora_negocio USING btree (esquema, tabla, id_registro, fecha_hora DESC);


--
-- Name: ix_bitacora_tabla; Type: INDEX; Schema: auditoria; Owner: -
--

CREATE INDEX ix_bitacora_tabla ON auditoria.bitacora_negocio USING btree (tabla, fecha_hora DESC);


--
-- Name: ix_bitacora_usuario; Type: INDEX; Schema: auditoria; Owner: -
--

CREATE INDEX ix_bitacora_usuario ON auditoria.bitacora_negocio USING btree (id_usuario, fecha_hora DESC);


--
-- Name: ix_novedad_empleado; Type: INDEX; Schema: novedades; Owner: -
--

CREATE INDEX ix_novedad_empleado ON novedades.novedad USING btree (id_empleado, inicio);


--
-- Name: ix_novedad_estado; Type: INDEX; Schema: novedades; Owner: -
--

CREATE INDEX ix_novedad_estado ON novedades.novedad USING btree (cod_estado_solicitud, registrado_en DESC);


--
-- Name: ix_novedad_rango; Type: INDEX; Schema: novedades; Owner: -
--

CREATE INDEX ix_novedad_rango ON novedades.novedad USING btree (inicio, fin);


--
-- Name: ix_periodo_vac_empleado; Type: INDEX; Schema: novedades; Owner: -
--

CREATE INDEX ix_periodo_vac_empleado ON novedades.periodo_vacacional USING btree (id_empleado);


--
-- Name: ix_vacaciones_empleado; Type: INDEX; Schema: novedades; Owner: -
--

CREATE INDEX ix_vacaciones_empleado ON novedades.solicitud_vacaciones USING btree (id_empleado, fecha_inicio);


--
-- Name: ix_vacaciones_estado; Type: INDEX; Schema: novedades; Owner: -
--

CREATE INDEX ix_vacaciones_estado ON novedades.solicitud_vacaciones USING btree (cod_estado_solicitud, solicitado_en DESC);


--
-- Name: ix_vacaciones_rango; Type: INDEX; Schema: novedades; Owner: -
--

CREATE INDEX ix_vacaciones_rango ON novedades.solicitud_vacaciones USING btree (fecha_inicio, fecha_fin);


--
-- Name: ix_area_sucursal; Type: INDEX; Schema: organizacion; Owner: -
--

CREATE INDEX ix_area_sucursal ON organizacion.area USING btree (id_sucursal);


--
-- Name: ix_empleado_area; Type: INDEX; Schema: organizacion; Owner: -
--

CREATE INDEX ix_empleado_area ON organizacion.empleado USING btree (id_area);


--
-- Name: ix_empleado_cargo; Type: INDEX; Schema: organizacion; Owner: -
--

CREATE INDEX ix_empleado_cargo ON organizacion.empleado USING btree (id_cargo);


--
-- Name: ix_parametro_valor_cod; Type: INDEX; Schema: parametrizacion; Owner: -
--

CREATE INDEX ix_parametro_valor_cod ON parametrizacion.parametro_valor USING btree (cod_parametro, vigente_desde DESC);


--
-- Name: ix_sesion_dispositivo_familia; Type: INDEX; Schema: seguridad; Owner: -
--

CREATE INDEX ix_sesion_dispositivo_familia ON seguridad.sesion_dispositivo USING btree (familia);


--
-- Name: ix_sesion_dispositivo_usuario; Type: INDEX; Schema: seguridad; Owner: -
--

CREATE INDEX ix_sesion_dispositivo_usuario ON seguridad.sesion_dispositivo USING btree (id_usuario) WHERE (revocada_en IS NULL);


--
-- Name: ix_usuario_rol; Type: INDEX; Schema: seguridad; Owner: -
--

CREATE INDEX ix_usuario_rol ON seguridad.usuario USING btree (id_rol);


--
-- Name: uq_solicitud_registro_pendiente; Type: INDEX; Schema: seguridad; Owner: -
--

CREATE UNIQUE INDEX uq_solicitud_registro_pendiente ON seguridad.solicitud_registro USING btree (lower((correo)::text)) WHERE ((cod_estado_solicitud)::text = 'PENDIENTE'::text);


--
-- Name: uq_usuario_correo; Type: INDEX; Schema: seguridad; Owner: -
--

CREATE UNIQUE INDEX uq_usuario_correo ON seguridad.usuario USING btree (lower((correo)::text));


--
-- Name: ix_asignacion_empleado; Type: INDEX; Schema: turnos; Owner: -
--

CREATE INDEX ix_asignacion_empleado ON turnos.asignacion USING btree (id_empleado, fecha);


--
-- Name: ix_asignacion_fecha; Type: INDEX; Schema: turnos; Owner: -
--

CREATE INDEX ix_asignacion_fecha ON turnos.asignacion USING btree (fecha);


--
-- Name: ix_asignacion_turno; Type: INDEX; Schema: turnos; Owner: -
--

CREATE INDEX ix_asignacion_turno ON turnos.asignacion USING btree (id_turno, fecha);


--
-- Name: ix_cambio_turno_companero; Type: INDEX; Schema: turnos; Owner: -
--

CREATE INDEX ix_cambio_turno_companero ON turnos.solicitud_cambio_turno USING btree (id_asignacion_companero);


--
-- Name: ix_cambio_turno_estado; Type: INDEX; Schema: turnos; Owner: -
--

CREATE INDEX ix_cambio_turno_estado ON turnos.solicitud_cambio_turno USING btree (cod_estado_solicitud, solicitado_en DESC);


--
-- Name: ix_cambio_turno_solicitante; Type: INDEX; Schema: turnos; Owner: -
--

CREATE INDEX ix_cambio_turno_solicitante ON turnos.solicitud_cambio_turno USING btree (id_asignacion_solicitante);


--
-- Name: registro tg_aud_registro; Type: TRIGGER; Schema: asistencia; Owner: -
--

CREATE TRIGGER tg_aud_registro AFTER INSERT OR DELETE OR UPDATE ON asistencia.registro FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_registro');


--
-- Name: bitacora_negocio tg_bitacora_inmutable; Type: TRIGGER; Schema: auditoria; Owner: -
--

CREATE TRIGGER tg_bitacora_inmutable BEFORE DELETE OR UPDATE ON auditoria.bitacora_negocio FOR EACH ROW EXECUTE FUNCTION auditoria.fn_bitacora_inmutable();


--
-- Name: novedad tg_aud_novedad; Type: TRIGGER; Schema: novedades; Owner: -
--

CREATE TRIGGER tg_aud_novedad AFTER INSERT OR DELETE OR UPDATE ON novedades.novedad FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_novedad');


--
-- Name: periodo_vacacional tg_aud_periodo_vacacional; Type: TRIGGER; Schema: novedades; Owner: -
--

CREATE TRIGGER tg_aud_periodo_vacacional AFTER INSERT OR DELETE OR UPDATE ON novedades.periodo_vacacional FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_periodo_vacacional');


--
-- Name: solicitud_vacaciones tg_aud_vacaciones; Type: TRIGGER; Schema: novedades; Owner: -
--

CREATE TRIGGER tg_aud_vacaciones AFTER INSERT OR DELETE OR UPDATE ON novedades.solicitud_vacaciones FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_solicitud_vacaciones');


--
-- Name: area tg_aud_area; Type: TRIGGER; Schema: organizacion; Owner: -
--

CREATE TRIGGER tg_aud_area AFTER INSERT OR DELETE OR UPDATE ON organizacion.area FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_area');


--
-- Name: cargo tg_aud_cargo; Type: TRIGGER; Schema: organizacion; Owner: -
--

CREATE TRIGGER tg_aud_cargo AFTER INSERT OR DELETE OR UPDATE ON organizacion.cargo FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_cargo');


--
-- Name: empleado tg_aud_empleado; Type: TRIGGER; Schema: organizacion; Owner: -
--

CREATE TRIGGER tg_aud_empleado AFTER INSERT OR DELETE OR UPDATE ON organizacion.empleado FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_empleado');


--
-- Name: sucursal tg_aud_sucursal; Type: TRIGGER; Schema: organizacion; Owner: -
--

CREATE TRIGGER tg_aud_sucursal AFTER INSERT OR DELETE OR UPDATE ON organizacion.sucursal FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_sucursal');


--
-- Name: dia_festivo tg_aud_dia_festivo; Type: TRIGGER; Schema: parametrizacion; Owner: -
--

CREATE TRIGGER tg_aud_dia_festivo AFTER INSERT OR DELETE OR UPDATE ON parametrizacion.dia_festivo FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('fecha');


--
-- Name: parametro tg_aud_parametro; Type: TRIGGER; Schema: parametrizacion; Owner: -
--

CREATE TRIGGER tg_aud_parametro AFTER INSERT OR DELETE OR UPDATE ON parametrizacion.parametro FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('cod_parametro');


--
-- Name: parametro_valor tg_aud_parametro_valor; Type: TRIGGER; Schema: parametrizacion; Owner: -
--

CREATE TRIGGER tg_aud_parametro_valor AFTER INSERT OR DELETE OR UPDATE ON parametrizacion.parametro_valor FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_parametro_valor');


--
-- Name: rol tg_aud_rol; Type: TRIGGER; Schema: seguridad; Owner: -
--

CREATE TRIGGER tg_aud_rol AFTER INSERT OR DELETE OR UPDATE ON seguridad.rol FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_rol');


--
-- Name: rol_permiso tg_aud_rol_permiso; Type: TRIGGER; Schema: seguridad; Owner: -
--

CREATE TRIGGER tg_aud_rol_permiso AFTER INSERT OR DELETE OR UPDATE ON seguridad.rol_permiso FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_rol');


--
-- Name: solicitud_registro tg_aud_solicitud_registro; Type: TRIGGER; Schema: seguridad; Owner: -
--

CREATE TRIGGER tg_aud_solicitud_registro AFTER INSERT OR DELETE OR UPDATE ON seguridad.solicitud_registro FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_solicitud_registro');


--
-- Name: usuario tg_aud_usuario; Type: TRIGGER; Schema: seguridad; Owner: -
--

CREATE TRIGGER tg_aud_usuario AFTER INSERT OR DELETE OR UPDATE ON seguridad.usuario FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_usuario');


--
-- Name: usuario_foto_perfil tg_aud_usuario_foto_perfil; Type: TRIGGER; Schema: seguridad; Owner: -
--

CREATE TRIGGER tg_aud_usuario_foto_perfil AFTER INSERT OR DELETE OR UPDATE ON seguridad.usuario_foto_perfil FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_usuario');


--
-- Name: asignacion tg_aud_asignacion; Type: TRIGGER; Schema: turnos; Owner: -
--

CREATE TRIGGER tg_aud_asignacion AFTER INSERT OR DELETE OR UPDATE ON turnos.asignacion FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_asignacion');


--
-- Name: dia_descanso tg_aud_dia_descanso; Type: TRIGGER; Schema: turnos; Owner: -
--

CREATE TRIGGER tg_aud_dia_descanso AFTER INSERT OR DELETE OR UPDATE ON turnos.dia_descanso FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_dia_descanso');


--
-- Name: solicitud_cambio_turno tg_aud_solicitud_cambio_turno; Type: TRIGGER; Schema: turnos; Owner: -
--

CREATE TRIGGER tg_aud_solicitud_cambio_turno AFTER INSERT OR DELETE OR UPDATE ON turnos.solicitud_cambio_turno FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_solicitud_cambio');


--
-- Name: turno tg_aud_turno; Type: TRIGGER; Schema: turnos; Owner: -
--

CREATE TRIGGER tg_aud_turno AFTER INSERT OR DELETE OR UPDATE ON turnos.turno FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_turno');


--
-- Name: asignacion tg_validar_asignacion; Type: TRIGGER; Schema: turnos; Owner: -
--

CREATE TRIGGER tg_validar_asignacion BEFORE INSERT OR UPDATE OF id_empleado, fecha, periodo, cod_estado_asignacion ON turnos.asignacion FOR EACH ROW EXECUTE FUNCTION turnos.fn_validar_asignacion();


--
-- Name: detalle_horas detalle_horas_cod_categoria_hora_fkey; Type: FK CONSTRAINT; Schema: asistencia; Owner: -
--

ALTER TABLE ONLY asistencia.detalle_horas
    ADD CONSTRAINT detalle_horas_cod_categoria_hora_fkey FOREIGN KEY (cod_categoria_hora) REFERENCES parametrizacion.categoria_hora(cod_categoria_hora);


--
-- Name: detalle_horas detalle_horas_id_registro_fkey; Type: FK CONSTRAINT; Schema: asistencia; Owner: -
--

ALTER TABLE ONLY asistencia.detalle_horas
    ADD CONSTRAINT detalle_horas_id_registro_fkey FOREIGN KEY (id_registro) REFERENCES asistencia.registro(id_registro);


--
-- Name: registro registro_cod_fuente_marcacion_fkey; Type: FK CONSTRAINT; Schema: asistencia; Owner: -
--

ALTER TABLE ONLY asistencia.registro
    ADD CONSTRAINT registro_cod_fuente_marcacion_fkey FOREIGN KEY (cod_fuente_marcacion) REFERENCES parametrizacion.fuente_marcacion(cod_fuente_marcacion);


--
-- Name: registro registro_id_asignacion_id_empleado_fkey; Type: FK CONSTRAINT; Schema: asistencia; Owner: -
--

ALTER TABLE ONLY asistencia.registro
    ADD CONSTRAINT registro_id_asignacion_id_empleado_fkey FOREIGN KEY (id_asignacion, id_empleado) REFERENCES turnos.asignacion(id_asignacion, id_empleado);


--
-- Name: registro registro_id_empleado_fkey; Type: FK CONSTRAINT; Schema: asistencia; Owner: -
--

ALTER TABLE ONLY asistencia.registro
    ADD CONSTRAINT registro_id_empleado_fkey FOREIGN KEY (id_empleado) REFERENCES organizacion.empleado(id_empleado);


--
-- Name: registro registro_registrado_por_fkey; Type: FK CONSTRAINT; Schema: asistencia; Owner: -
--

ALTER TABLE ONLY asistencia.registro
    ADD CONSTRAINT registro_registrado_por_fkey FOREIGN KEY (registrado_por) REFERENCES seguridad.usuario(id_usuario);


--
-- Name: bitacora_negocio bitacora_negocio_cod_tipo_accion_fkey; Type: FK CONSTRAINT; Schema: auditoria; Owner: -
--

ALTER TABLE ONLY auditoria.bitacora_negocio
    ADD CONSTRAINT bitacora_negocio_cod_tipo_accion_fkey FOREIGN KEY (cod_tipo_accion) REFERENCES parametrizacion.tipo_accion(cod_tipo_accion);


--
-- Name: bitacora_negocio bitacora_negocio_id_empleado_fkey; Type: FK CONSTRAINT; Schema: auditoria; Owner: -
--

ALTER TABLE ONLY auditoria.bitacora_negocio
    ADD CONSTRAINT bitacora_negocio_id_empleado_fkey FOREIGN KEY (id_empleado) REFERENCES organizacion.empleado(id_empleado);


--
-- Name: bitacora_negocio bitacora_negocio_id_usuario_fkey; Type: FK CONSTRAINT; Schema: auditoria; Owner: -
--

ALTER TABLE ONLY auditoria.bitacora_negocio
    ADD CONSTRAINT bitacora_negocio_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES seguridad.usuario(id_usuario);


--
-- Name: novedad novedad_cod_estado_solicitud_fkey; Type: FK CONSTRAINT; Schema: novedades; Owner: -
--

ALTER TABLE ONLY novedades.novedad
    ADD CONSTRAINT novedad_cod_estado_solicitud_fkey FOREIGN KEY (cod_estado_solicitud) REFERENCES parametrizacion.estado_solicitud(cod_estado_solicitud);


--
-- Name: novedad novedad_cod_tipo_novedad_fkey; Type: FK CONSTRAINT; Schema: novedades; Owner: -
--

ALTER TABLE ONLY novedades.novedad
    ADD CONSTRAINT novedad_cod_tipo_novedad_fkey FOREIGN KEY (cod_tipo_novedad) REFERENCES parametrizacion.tipo_novedad(cod_tipo_novedad);


--
-- Name: novedad novedad_id_empleado_fkey; Type: FK CONSTRAINT; Schema: novedades; Owner: -
--

ALTER TABLE ONLY novedades.novedad
    ADD CONSTRAINT novedad_id_empleado_fkey FOREIGN KEY (id_empleado) REFERENCES organizacion.empleado(id_empleado);


--
-- Name: novedad novedad_registrado_por_fkey; Type: FK CONSTRAINT; Schema: novedades; Owner: -
--

ALTER TABLE ONLY novedades.novedad
    ADD CONSTRAINT novedad_registrado_por_fkey FOREIGN KEY (registrado_por) REFERENCES seguridad.usuario(id_usuario);


--
-- Name: novedad novedad_resuelto_por_fkey; Type: FK CONSTRAINT; Schema: novedades; Owner: -
--

ALTER TABLE ONLY novedades.novedad
    ADD CONSTRAINT novedad_resuelto_por_fkey FOREIGN KEY (resuelto_por) REFERENCES seguridad.usuario(id_usuario);


--
-- Name: periodo_vacacional periodo_vacacional_id_empleado_fkey; Type: FK CONSTRAINT; Schema: novedades; Owner: -
--

ALTER TABLE ONLY novedades.periodo_vacacional
    ADD CONSTRAINT periodo_vacacional_id_empleado_fkey FOREIGN KEY (id_empleado) REFERENCES organizacion.empleado(id_empleado);


--
-- Name: solicitud_vacaciones solicitud_vacaciones_cod_estado_solicitud_fkey; Type: FK CONSTRAINT; Schema: novedades; Owner: -
--

ALTER TABLE ONLY novedades.solicitud_vacaciones
    ADD CONSTRAINT solicitud_vacaciones_cod_estado_solicitud_fkey FOREIGN KEY (cod_estado_solicitud) REFERENCES parametrizacion.estado_solicitud(cod_estado_solicitud);


--
-- Name: solicitud_vacaciones solicitud_vacaciones_id_empleado_fkey; Type: FK CONSTRAINT; Schema: novedades; Owner: -
--

ALTER TABLE ONLY novedades.solicitud_vacaciones
    ADD CONSTRAINT solicitud_vacaciones_id_empleado_fkey FOREIGN KEY (id_empleado) REFERENCES organizacion.empleado(id_empleado);


--
-- Name: solicitud_vacaciones solicitud_vacaciones_resuelto_por_fkey; Type: FK CONSTRAINT; Schema: novedades; Owner: -
--

ALTER TABLE ONLY novedades.solicitud_vacaciones
    ADD CONSTRAINT solicitud_vacaciones_resuelto_por_fkey FOREIGN KEY (resuelto_por) REFERENCES seguridad.usuario(id_usuario);


--
-- Name: solicitud_vacaciones solicitud_vacaciones_solicitado_por_fkey; Type: FK CONSTRAINT; Schema: novedades; Owner: -
--

ALTER TABLE ONLY novedades.solicitud_vacaciones
    ADD CONSTRAINT solicitud_vacaciones_solicitado_por_fkey FOREIGN KEY (solicitado_por) REFERENCES seguridad.usuario(id_usuario);


--
-- Name: area area_id_sucursal_fkey; Type: FK CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.area
    ADD CONSTRAINT area_id_sucursal_fkey FOREIGN KEY (id_sucursal) REFERENCES organizacion.sucursal(id_sucursal);


--
-- Name: empleado empleado_cod_estado_empleado_fkey; Type: FK CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.empleado
    ADD CONSTRAINT empleado_cod_estado_empleado_fkey FOREIGN KEY (cod_estado_empleado) REFERENCES parametrizacion.estado_empleado(cod_estado_empleado);


--
-- Name: empleado empleado_cod_tipo_contrato_fkey; Type: FK CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.empleado
    ADD CONSTRAINT empleado_cod_tipo_contrato_fkey FOREIGN KEY (cod_tipo_contrato) REFERENCES parametrizacion.tipo_contrato(cod_tipo_contrato);


--
-- Name: empleado empleado_cod_tipo_documento_fkey; Type: FK CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.empleado
    ADD CONSTRAINT empleado_cod_tipo_documento_fkey FOREIGN KEY (cod_tipo_documento) REFERENCES parametrizacion.tipo_documento(cod_tipo_documento);


--
-- Name: empleado empleado_id_area_fkey; Type: FK CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.empleado
    ADD CONSTRAINT empleado_id_area_fkey FOREIGN KEY (id_area) REFERENCES organizacion.area(id_area);


--
-- Name: empleado empleado_id_cargo_fkey; Type: FK CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.empleado
    ADD CONSTRAINT empleado_id_cargo_fkey FOREIGN KEY (id_cargo) REFERENCES organizacion.cargo(id_cargo);


--
-- Name: empleado empleado_id_usuario_fkey; Type: FK CONSTRAINT; Schema: organizacion; Owner: -
--

ALTER TABLE ONLY organizacion.empleado
    ADD CONSTRAINT empleado_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES seguridad.usuario(id_usuario);


--
-- Name: parametro_valor parametro_valor_cod_parametro_fkey; Type: FK CONSTRAINT; Schema: parametrizacion; Owner: -
--

ALTER TABLE ONLY parametrizacion.parametro_valor
    ADD CONSTRAINT parametro_valor_cod_parametro_fkey FOREIGN KEY (cod_parametro) REFERENCES parametrizacion.parametro(cod_parametro);


--
-- Name: rol_permiso rol_permiso_id_permiso_fkey; Type: FK CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.rol_permiso
    ADD CONSTRAINT rol_permiso_id_permiso_fkey FOREIGN KEY (id_permiso) REFERENCES seguridad.permiso(id_permiso);


--
-- Name: rol_permiso rol_permiso_id_rol_fkey; Type: FK CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.rol_permiso
    ADD CONSTRAINT rol_permiso_id_rol_fkey FOREIGN KEY (id_rol) REFERENCES seguridad.rol(id_rol);


--
-- Name: sesion_dispositivo sesion_dispositivo_id_usuario_fkey; Type: FK CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.sesion_dispositivo
    ADD CONSTRAINT sesion_dispositivo_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES seguridad.usuario(id_usuario);


--
-- Name: solicitud_registro solicitud_registro_cod_estado_solicitud_fkey; Type: FK CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.solicitud_registro
    ADD CONSTRAINT solicitud_registro_cod_estado_solicitud_fkey FOREIGN KEY (cod_estado_solicitud) REFERENCES parametrizacion.estado_solicitud(cod_estado_solicitud);


--
-- Name: solicitud_registro solicitud_registro_cod_tipo_documento_fkey; Type: FK CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.solicitud_registro
    ADD CONSTRAINT solicitud_registro_cod_tipo_documento_fkey FOREIGN KEY (cod_tipo_documento) REFERENCES parametrizacion.tipo_documento(cod_tipo_documento);


--
-- Name: solicitud_registro solicitud_registro_id_usuario_creado_fkey; Type: FK CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.solicitud_registro
    ADD CONSTRAINT solicitud_registro_id_usuario_creado_fkey FOREIGN KEY (id_usuario_creado) REFERENCES seguridad.usuario(id_usuario);


--
-- Name: solicitud_registro solicitud_registro_revisado_por_fkey; Type: FK CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.solicitud_registro
    ADD CONSTRAINT solicitud_registro_revisado_por_fkey FOREIGN KEY (revisado_por) REFERENCES seguridad.usuario(id_usuario);


--
-- Name: usuario usuario_cod_estado_usuario_fkey; Type: FK CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.usuario
    ADD CONSTRAINT usuario_cod_estado_usuario_fkey FOREIGN KEY (cod_estado_usuario) REFERENCES parametrizacion.estado_usuario(cod_estado_usuario);


--
-- Name: usuario_foto_perfil usuario_foto_perfil_id_usuario_fkey; Type: FK CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.usuario_foto_perfil
    ADD CONSTRAINT usuario_foto_perfil_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES seguridad.usuario(id_usuario);


--
-- Name: usuario usuario_id_rol_fkey; Type: FK CONSTRAINT; Schema: seguridad; Owner: -
--

ALTER TABLE ONLY seguridad.usuario
    ADD CONSTRAINT usuario_id_rol_fkey FOREIGN KEY (id_rol) REFERENCES seguridad.rol(id_rol);


--
-- Name: asignacion asignacion_cod_estado_asignacion_fkey; Type: FK CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.asignacion
    ADD CONSTRAINT asignacion_cod_estado_asignacion_fkey FOREIGN KEY (cod_estado_asignacion) REFERENCES parametrizacion.estado_asignacion(cod_estado_asignacion);


--
-- Name: asignacion asignacion_creado_por_fkey; Type: FK CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.asignacion
    ADD CONSTRAINT asignacion_creado_por_fkey FOREIGN KEY (creado_por) REFERENCES seguridad.usuario(id_usuario);


--
-- Name: asignacion asignacion_id_empleado_fkey; Type: FK CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.asignacion
    ADD CONSTRAINT asignacion_id_empleado_fkey FOREIGN KEY (id_empleado) REFERENCES organizacion.empleado(id_empleado);


--
-- Name: asignacion asignacion_id_turno_fkey; Type: FK CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.asignacion
    ADD CONSTRAINT asignacion_id_turno_fkey FOREIGN KEY (id_turno) REFERENCES turnos.turno(id_turno);


--
-- Name: dia_descanso dia_descanso_creado_por_fkey; Type: FK CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.dia_descanso
    ADD CONSTRAINT dia_descanso_creado_por_fkey FOREIGN KEY (creado_por) REFERENCES seguridad.usuario(id_usuario);


--
-- Name: dia_descanso dia_descanso_id_empleado_fkey; Type: FK CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.dia_descanso
    ADD CONSTRAINT dia_descanso_id_empleado_fkey FOREIGN KEY (id_empleado) REFERENCES organizacion.empleado(id_empleado);


--
-- Name: solicitud_cambio_turno solicitud_cambio_turno_cod_estado_solicitud_fkey; Type: FK CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.solicitud_cambio_turno
    ADD CONSTRAINT solicitud_cambio_turno_cod_estado_solicitud_fkey FOREIGN KEY (cod_estado_solicitud) REFERENCES parametrizacion.estado_solicitud(cod_estado_solicitud);


--
-- Name: solicitud_cambio_turno solicitud_cambio_turno_id_asignacion_companero_fkey; Type: FK CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.solicitud_cambio_turno
    ADD CONSTRAINT solicitud_cambio_turno_id_asignacion_companero_fkey FOREIGN KEY (id_asignacion_companero) REFERENCES turnos.asignacion(id_asignacion);


--
-- Name: solicitud_cambio_turno solicitud_cambio_turno_id_asignacion_solicitante_fkey; Type: FK CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.solicitud_cambio_turno
    ADD CONSTRAINT solicitud_cambio_turno_id_asignacion_solicitante_fkey FOREIGN KEY (id_asignacion_solicitante) REFERENCES turnos.asignacion(id_asignacion);


--
-- Name: solicitud_cambio_turno solicitud_cambio_turno_resuelto_por_fkey; Type: FK CONSTRAINT; Schema: turnos; Owner: -
--

ALTER TABLE ONLY turnos.solicitud_cambio_turno
    ADD CONSTRAINT solicitud_cambio_turno_resuelto_por_fkey FOREIGN KEY (resuelto_por) REFERENCES seguridad.usuario(id_usuario);


--
-- Name: detalle_horas; Type: ROW SECURITY; Schema: asistencia; Owner: -
--

ALTER TABLE asistencia.detalle_horas ENABLE ROW LEVEL SECURITY;

--
-- Name: registro; Type: ROW SECURITY; Schema: asistencia; Owner: -
--

ALTER TABLE asistencia.registro ENABLE ROW LEVEL SECURITY;

--
-- Name: bitacora_negocio; Type: ROW SECURITY; Schema: auditoria; Owner: -
--

ALTER TABLE auditoria.bitacora_negocio ENABLE ROW LEVEL SECURITY;

--
-- Name: novedad; Type: ROW SECURITY; Schema: novedades; Owner: -
--

ALTER TABLE novedades.novedad ENABLE ROW LEVEL SECURITY;

--
-- Name: periodo_vacacional; Type: ROW SECURITY; Schema: novedades; Owner: -
--

ALTER TABLE novedades.periodo_vacacional ENABLE ROW LEVEL SECURITY;

--
-- Name: solicitud_vacaciones; Type: ROW SECURITY; Schema: novedades; Owner: -
--

ALTER TABLE novedades.solicitud_vacaciones ENABLE ROW LEVEL SECURITY;

--
-- Name: area; Type: ROW SECURITY; Schema: organizacion; Owner: -
--

ALTER TABLE organizacion.area ENABLE ROW LEVEL SECURITY;

--
-- Name: cargo; Type: ROW SECURITY; Schema: organizacion; Owner: -
--

ALTER TABLE organizacion.cargo ENABLE ROW LEVEL SECURITY;

--
-- Name: empleado; Type: ROW SECURITY; Schema: organizacion; Owner: -
--

ALTER TABLE organizacion.empleado ENABLE ROW LEVEL SECURITY;

--
-- Name: sucursal; Type: ROW SECURITY; Schema: organizacion; Owner: -
--

ALTER TABLE organizacion.sucursal ENABLE ROW LEVEL SECURITY;

--
-- Name: categoria_hora; Type: ROW SECURITY; Schema: parametrizacion; Owner: -
--

ALTER TABLE parametrizacion.categoria_hora ENABLE ROW LEVEL SECURITY;

--
-- Name: dia_festivo; Type: ROW SECURITY; Schema: parametrizacion; Owner: -
--

ALTER TABLE parametrizacion.dia_festivo ENABLE ROW LEVEL SECURITY;

--
-- Name: estado_asignacion; Type: ROW SECURITY; Schema: parametrizacion; Owner: -
--

ALTER TABLE parametrizacion.estado_asignacion ENABLE ROW LEVEL SECURITY;

--
-- Name: estado_empleado; Type: ROW SECURITY; Schema: parametrizacion; Owner: -
--

ALTER TABLE parametrizacion.estado_empleado ENABLE ROW LEVEL SECURITY;

--
-- Name: estado_solicitud; Type: ROW SECURITY; Schema: parametrizacion; Owner: -
--

ALTER TABLE parametrizacion.estado_solicitud ENABLE ROW LEVEL SECURITY;

--
-- Name: estado_usuario; Type: ROW SECURITY; Schema: parametrizacion; Owner: -
--

ALTER TABLE parametrizacion.estado_usuario ENABLE ROW LEVEL SECURITY;

--
-- Name: fuente_marcacion; Type: ROW SECURITY; Schema: parametrizacion; Owner: -
--

ALTER TABLE parametrizacion.fuente_marcacion ENABLE ROW LEVEL SECURITY;

--
-- Name: parametro; Type: ROW SECURITY; Schema: parametrizacion; Owner: -
--

ALTER TABLE parametrizacion.parametro ENABLE ROW LEVEL SECURITY;

--
-- Name: parametro_valor; Type: ROW SECURITY; Schema: parametrizacion; Owner: -
--

ALTER TABLE parametrizacion.parametro_valor ENABLE ROW LEVEL SECURITY;

--
-- Name: tipo_accion; Type: ROW SECURITY; Schema: parametrizacion; Owner: -
--

ALTER TABLE parametrizacion.tipo_accion ENABLE ROW LEVEL SECURITY;

--
-- Name: tipo_contrato; Type: ROW SECURITY; Schema: parametrizacion; Owner: -
--

ALTER TABLE parametrizacion.tipo_contrato ENABLE ROW LEVEL SECURITY;

--
-- Name: tipo_documento; Type: ROW SECURITY; Schema: parametrizacion; Owner: -
--

ALTER TABLE parametrizacion.tipo_documento ENABLE ROW LEVEL SECURITY;

--
-- Name: tipo_novedad; Type: ROW SECURITY; Schema: parametrizacion; Owner: -
--

ALTER TABLE parametrizacion.tipo_novedad ENABLE ROW LEVEL SECURITY;

--
-- Name: permiso; Type: ROW SECURITY; Schema: seguridad; Owner: -
--

ALTER TABLE seguridad.permiso ENABLE ROW LEVEL SECURITY;

--
-- Name: rol; Type: ROW SECURITY; Schema: seguridad; Owner: -
--

ALTER TABLE seguridad.rol ENABLE ROW LEVEL SECURITY;

--
-- Name: rol_permiso; Type: ROW SECURITY; Schema: seguridad; Owner: -
--

ALTER TABLE seguridad.rol_permiso ENABLE ROW LEVEL SECURITY;

--
-- Name: solicitud_registro; Type: ROW SECURITY; Schema: seguridad; Owner: -
--

ALTER TABLE seguridad.solicitud_registro ENABLE ROW LEVEL SECURITY;

--
-- Name: usuario; Type: ROW SECURITY; Schema: seguridad; Owner: -
--

ALTER TABLE seguridad.usuario ENABLE ROW LEVEL SECURITY;

--
-- Name: asignacion; Type: ROW SECURITY; Schema: turnos; Owner: -
--

ALTER TABLE turnos.asignacion ENABLE ROW LEVEL SECURITY;

--
-- Name: dia_descanso; Type: ROW SECURITY; Schema: turnos; Owner: -
--

ALTER TABLE turnos.dia_descanso ENABLE ROW LEVEL SECURITY;

--
-- Name: solicitud_cambio_turno; Type: ROW SECURITY; Schema: turnos; Owner: -
--

ALTER TABLE turnos.solicitud_cambio_turno ENABLE ROW LEVEL SECURITY;

--
-- Name: turno; Type: ROW SECURITY; Schema: turnos; Owner: -
--

ALTER TABLE turnos.turno ENABLE ROW LEVEL SECURITY;

--
-- PostgreSQL database dump complete
--



-- Permisos para el rol de la aplicación (solo si existe en esta base)
DO $$
DECLARE esquema text;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gestion_turnos_app') THEN
    FOREACH esquema IN ARRAY ARRAY['asistencia','auditoria','novedades','organizacion','parametrizacion','seguridad','turnos'] LOOP
      EXECUTE format('GRANT USAGE ON SCHEMA %I TO gestion_turnos_app', esquema);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO gestion_turnos_app', esquema);
      EXECUTE format('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO gestion_turnos_app', esquema);
    END LOOP;
  END IF;
END;
$$;

COMMIT;
