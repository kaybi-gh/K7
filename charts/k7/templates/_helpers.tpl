{{- define "k7.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "k7.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name (include "k7.name" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}

{{- define "k7.labels" -}}
app.kubernetes.io/name: {{ include "k7.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" }}
{{- with .Chart.AppVersion }}
app.kubernetes.io/version: {{ . | quote }}
{{- end }}
{{- end -}}

{{- define "k7.selectorLabels" -}}
app.kubernetes.io/name: {{ include "k7.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}

{{- define "k7.cnpg.clusterName" -}}
{{- printf "%s-cnpg" (include "k7.fullname" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "k7.postgres.serviceName" -}}
{{- printf "%s-postgres" (include "k7.fullname" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- /* Secret holding the DB password. CNPG generates <cluster>-app; other modes use the chart/existing secret. */ -}}
{{- define "k7.database.secretName" -}}
{{- if eq .Values.database.mode "cnpg" -}}
{{- printf "%s-app" (include "k7.cnpg.clusterName" .) -}}
{{- else if eq .Values.database.mode "deployment" -}}
{{- default (include "k7.fullname" .) .Values.database.deployment.existingSecret -}}
{{- else -}}
{{- default (include "k7.fullname" .) .Values.database.external.existingSecret -}}
{{- end -}}
{{- end -}}

{{- define "k7.database.passwordKey" -}}
{{- if eq .Values.database.mode "cnpg" -}}password{{- else -}}database-password{{- end -}}
{{- end -}}

{{- define "k7.database.host" -}}
{{- if eq .Values.database.mode "cnpg" -}}
{{- printf "%s-rw" (include "k7.cnpg.clusterName" .) -}}
{{- else if eq .Values.database.mode "deployment" -}}
{{- include "k7.postgres.serviceName" . -}}
{{- else -}}
{{- .Values.database.external.host -}}
{{- end -}}
{{- end -}}

{{- define "k7.security.secretName" -}}
{{- default (include "k7.fullname" .) .Values.security.existingSecret -}}
{{- end -}}

{{- define "k7.pvcName" -}}
{{- default (include "k7.fullname" .) .Values.persistence.existingClaim -}}
{{- end -}}
