# Fixes missing French diacritics in default (fr) .resx value strings only.
# Does not modify resource keys (name="...") or .en.resx files.
# Prefer fix-french-resx-accents.py when Python is available (UTF-8 safe).
# After running this script on Windows PowerShell, run fix-resx-mojibake.ps1 if needed.
# Usage: pwsh scripts/fix-french-resx-accents.ps1 [-WhatIf]

param(
    [switch]$WhatIf,
    [string]$Root = (Join-Path $PSScriptRoot "..\src\Clients\Shared\UI\Resources")
)

function Fix-Mojibake {
    param([string]$Value)
    if ($Value -notmatch 'Ã') {
        return $Value
    }

    try {
        $bytes = [System.Text.Encoding]::GetEncoding('ISO-8859-1').GetBytes($Value)
        $fixed = [System.Text.Encoding]::UTF8.GetString($bytes)
        if ($fixed -notmatch 'Ã') {
            return $fixed
        }
    }
    catch {
        # fall through to literal replacements
    }

    $map = @{
        'Ã©' = 'é'
        'Ã¨' = 'è'
        'Ãª' = 'ê'
        'Ã«' = 'ë'
        'Ã§' = 'ç'
        'Ã®' = 'î'
        'Ã´' = 'ô'
        'Ã¹' = 'ù'
        'Ã‰' = 'É'
    }
    foreach ($key in $map.Keys) {
        $Value = $Value.Replace($key, $map[$key])
    }
    return $Value
}

$replacements = @(
    @('Experience par defaut', 'Expérience par défaut'),
    @('valeurs par defaut', 'valeurs par défaut'),
    @('Reinitialiser par defaut', 'Réinitialiser par défaut'),
    @('Par defaut', 'Par défaut'),
    @('par defaut', 'par défaut'),
    @('Reinitialiser', 'Réinitialiser'),
    @('reinitialisees', 'réinitialisées'),
    @('reinitialise', 'réinitialisé'),
    @('reinitialis', 'réinitialis'),
    @('Ces parametres definissent', 'Ces paramètres définissent'),
    @('parametres de lecture', 'paramètres de lecture'),
    @('parametres de', 'paramètres de'),
    @('Parametres', 'Paramètres'),
    @('parametres', 'paramètres'),
    @('parametre', 'paramètre'),
    @('Theme par défaut', 'Thème par défaut'),
    @('Theme par defaut', 'Thème par défaut'),
    @('Theme utilise', 'Thème utilisé'),
    @('Theme utilisé', 'Thème utilisé'),
    @('Langue utilisee', 'Langue utilisée'),
    @('Preferences enregistrees', 'Préférences enregistrées'),
    @('Preferences reinitialisees', 'Préférences réinitialisées'),
    @('Preferences serveur enregistrees', 'Préférences serveur enregistrées'),
    @('Preferences serveur reinitialisees', 'Préférences réinitialisées'),
    @('Preferences par defaut', 'Préférences par défaut'),
    @('Preferences', 'Préférences'),
    @('preferences', 'préférences'),
    @('Systeme', 'Système'),
    @('Activite', 'Activité'),
    @('desactivee pour la bibliotheque concernee', 'désactivée pour la bibliothèque concernée'),
    @('desactivee sur le serveur', 'désactivée sur le serveur'),
    @('desactivee', 'désactivée'),
    @('Desactives', 'Désactivés'),
    @('Desactive', 'Désactivé'),
    @('desactive', 'désactivé'),
    @('Generation des miniatures', 'Génération des miniatures'),
    @('generation des miniatures', 'génération des miniatures'),
    @('Generation', 'Génération'),
    @('generation', 'génération'),
    @('Opacite de l''arriere-plan', 'Opacité de l''arrière-plan'),
    @('Opacite', 'Opacité'),
    @('arriere-plan', 'arrière-plan'),
    @('bibliotheque concernee', 'bibliothèque concernée'),
    @('Bibliotheques a partager', 'Bibliothèques à partager'),
    @('Bibliotheques', 'Bibliothèques'),
    @('bibliotheque', 'bibliothèque'),
    @('Bibliotheque', 'Bibliothèque'),
    @('Detection d''intros', 'Détection d''intros'),
    @('Detection', 'Détection'),
    @('Detecter les intros', 'Détecter les intros'),
    @('Detecte automatiquement', 'Détecte automatiquement'),
    @('Detecte', 'Détecte'),
    @('metadonnees', 'métadonnées'),
    @('Metadonnees', 'Métadonnées'),
    @('Rafraichissement des metadonnees', 'Rafraîchissement des métadonnées'),
    @('Rafraichissement', 'Rafraîchissement'),
    @('Rafraichir', 'Rafraîchir'),
    @('regle "', 'règle "'),
    @('Regle ', 'Règle '),
    @('regle', 'règle'),
    @('donnees locales', 'données locales'),
    @('donnees de flux', 'données de flux'),
    @('donnees', 'données'),
    @('Aucune donnee', 'Aucune donnée'),
    @('Langue audio preferee', 'Langue audio préférée'),
    @('audio preferee', 'audio préférée'),
    @('preferee', 'préférée'),
    @('prefere', 'préféré'),
    @('Portee', 'Portée'),
    @('Lecture video', 'Lecture vidéo'),
    @('Lecteur video', 'Lecteur vidéo'),
    @('Codec video', 'Codec vidéo'),
    @('Type de media', 'Type de média'),
    @('Creation de media', 'Création de média'),
    @('Top medias', 'Top médias'),
    @('Voir le media', 'Voir le média'),
    @('Go utilises /', 'Go utilisés /'),
    @('utilises', 'utilisés'),
    @('episode suivant', 'épisode suivant'),
    @('episodes de series', 'épisodes de séries'),
    @('Hors-serie', 'Hors-série'),
    @('Saison precedente', 'Saison précédente'),
    @('Saison suivante', 'Saison suivante'),
    @('Intro passee', 'Intro passée'),
    @('Outro passee', 'Outro passée'),
    @('Resolution non supportee', 'Résolution non supportée'),
    @('Resolution', 'Résolution'),
    @('supportee', 'supportée'),
    @('Taches de fond', 'Tâches de fond'),
    @('Taches actives', 'Tâches actives'),
    @('Voir les taches', 'Voir les tâches'),
    @('en tache de fond', 'en tâche de fond'),
    @('tache de fond', 'tâche de fond'),
    @('Tache supprimee', 'Tâche supprimée'),
    @('Tache', 'Tâche'),
    @('taches', 'tâches'),
    @('tache', 'tâche'),
    @('Sante serveur', 'Santé serveur'),
    @('Sante', 'Santé'),
    @('Federation', 'Fédération'),
    @('federation', 'fédération'),
    @('Fonctionnalites', 'Fonctionnalités'),
    @('Fonctionnalite', 'Fonctionnalité'),
    @('irreversible', 'irréversible'),
    @('Acces aux donnees', 'Accès aux données'),
    @('Acces aux librairies', 'Accès aux librairies'),
    @('Acces utilisateurs', 'Accès utilisateurs'),
    @('Acces -', 'Accès -'),
    @('Demander un acces', 'Demander un accès'),
    @('l''acces a', 'l''accès à'),
    @('l''acces ', 'l''accès '),
    @(' avoir acces', ' avoir accès'),
    @('Pistes par defaut', 'Pistes par défaut'),
    @('defaut serveur', 'défaut serveur'),
    @('Audio et sous-titres (defaut serveur)', 'Audio et sous-titres (défaut serveur)'),
    @('Global (toutes les librairies)', 'Global (toutes les bibliothèques)'),
    @('Selectionnez', 'Sélectionnez'),
    @('Decochez', 'Décochez'),
    @('Definissez vos', 'Définissez vos'),
    @('Verifiez votre', 'Vérifiez votre'),
    @('Numero de piste', 'Numéro de piste'),
    @('Numero de disque', 'Numéro de disque'),
    @('Numero ', 'Numéro '),
    @('Duree (min)', 'Durée (min)'),
    @('Duree', 'Durée'),
    @('duree du fondu', 'durée du fondu'),
    @('La duree', 'La durée'),
    @('Deverrouiller', 'Déverrouiller'),
    @('empeche l''ecrasement', 'empêche l''écrasement'),
    @('empecher le rafra', 'empêcher le rafra'),
    @('empeche la saturation', 'empêche la saturation'),
    @('empeche', 'empêche'),
    @('empecher', 'empêcher'),
    @('ecrasement', 'écrasement'),
    @('ecraser', 'écraser'),
    @('egaliseur', 'égaliseur'),
    @('plein ecran', 'plein écran'),
    @('ecran', 'écran'),
    @('Ecran', 'Écran'),
    @('telechargement', 'téléchargement'),
    @('Telechargement', 'Téléchargement'),
    @('Telechargez', 'Téléchargez'),
    @('telecharge', 'télécharge'),
    @('ecouter', 'écouter'),
    @('etablir une connexion', 'établir une connexion'),
    @('etablir', 'établir'),
    @('routes de fédération sont', 'routes de fédération sont'),
    @('routes fédération sont', 'routes de fédération sont'),
    @('bloquees', 'bloquées'),
    @('gerer les pairs', 'gérer les pairs'),
    @('gerer', 'gérer'),
    @('integrer localement', 'intégrer localement'),
    @('integrer', 'intégrer'),
    @('partagees avec', 'partagées avec'),
    @('partagees', 'partagées'),
    @('partages existants', 'partages existants'),
    @('envoyee', 'envoyée'),
    @('envoye', 'envoyé'),
    @('creee', 'créée'),
    @('cree avec', 'créé avec'),
    @('supprimee', 'supprimée'),
    @('supprime avec', 'supprimé avec'),
    @('importee', 'importée'),
    @('importe avec', 'importé avec'),
    @('modifiee', 'modifiée'),
    @('avec succes', 'avec succès'),
    @('succes', 'succès'),
    @('selection automatique', 'sélection automatique'),
    @('de selection', 'de sélection'),
    @('la selection', 'la sélection'),
    @('Importer la selection', 'Importer la sélection'),
    @('doivent etre vraies', 'doivent être vraies'),
    @('Vous etes hors-ligne', 'Vous êtes hors-ligne'),
    @('avez ete expulse', 'avez été expulsé'),
    @('A propos', 'À propos'),
    @('Masques', 'Masqués'),
    @('mot de passe defini', 'mot de passe défini'),
    @('PIN defini', 'PIN défini'),
    @('est defini', 'est défini'),
    @('pas defini', 'pas défini'),
    @('Aucun PIN defini', 'Aucun PIN défini'),
    @('Aucun mot de passe defini', 'Aucun mot de passe défini'),
    @(' en definir ', ' en définir '),
    @('protege l''accès', 'protège l''accès'),
    @('protege', 'protège'),
    @('appareils partages', 'appareils partagés'),
    @('L''application à rencontré', 'L''application a rencontré'),
    @(' à expiré', ' a expiré'),
    @(' les partagés existants', ' les partages existants'),
    @('>Video<', '>Vidéo<'),
    @('>General<', '>Général<'),
    @('configuree', 'configurée'),
    @('pair configure', 'pair configuré'),
    @('Apercu', 'Aperçu'),
    @('Aucun element', 'Aucun élément'),
    @('elements', 'éléments'),
    @('revocation', 'révocation'),
    @('Frequence', 'Fréquence'),
    @('echantillonnage', 'échantillonnage'),
    @('lancee', 'lancée'),
    @('Derniere', 'Dernière'),
    @('calculee', 'calculée'),
    @('Egaliseur', 'Égaliseur'),
    @('Electronique', 'Électronique'),
    @('enregistres', 'enregistrés'),
    @('evenements', 'événements'),
    @('evenement', 'événement'),
    @('Selection des', 'Sélection des'),
    @('Stockage utilise', 'Stockage utilisé'),
    @('Preparation', 'Préparation'),
    @('Creez', 'Créez'),
    @('lancee(s)', 'lancée(s)'),
    @('Derniere ecoute', 'Dernière écoute'),
    @('ecoute', 'écoute')
)

function Fix-FrenchValue {
    param([string]$Value)
    $Value = Fix-Mojibake $Value
    foreach ($pair in $replacements) {
        $Value = $Value.Replace($pair[0], $pair[1])
    }
    return $Value
}

$files = Get-ChildItem -Path $Root -Filter "*.resx" -Recurse |
    Where-Object { $_.Name -notmatch '\.en\.resx$' }

$changedFiles = 0
$changedValues = 0

foreach ($file in $files) {
    $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.UTF8Encoding]::new($false))
    $original = $content
    $fileChanges = 0

    $updated = [regex]::Replace($content, '(<value[^>]*>)(.*?)(</value>)', {
        param($match)
        $prefix = $match.Groups[1].Value
        $value = $match.Groups[2].Value
        $suffix = $match.Groups[3].Value
        $fixed = Fix-FrenchValue $value
        if ($fixed -ne $value) {
            $script:fileChanges++
        }
        return "$prefix$fixed$suffix"
    })

    if ($updated -ne $original) {
        $changedFiles++
        $changedValues += $fileChanges
        if (-not $WhatIf) {
            [System.IO.File]::WriteAllText($file.FullName, $updated, [System.Text.UTF8Encoding]::new($false))
        }
        Write-Output "$($file.FullName.Replace($Root, '.')) ($fileChanges values)"
    }
}

$summarySuffix = if ($WhatIf) { ' (WhatIf)' } else { '' }
Write-Output ""
Write-Output "Changed $changedFiles files, $changedValues value strings$summarySuffix."
