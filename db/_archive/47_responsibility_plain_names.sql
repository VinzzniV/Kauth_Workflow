UPDATE app_responsibilities
SET name = CASE responsibility_key
    WHEN 'it_ad' THEN 'AD'
    WHEN 'it_mailbox' THEN 'Mailbox'
    WHEN 'it_habel' THEN 'Habel'
    WHEN 'it_ln' THEN 'LN'
    WHEN 'it_hardware' THEN 'Hardware'
    WHEN 'qs_babtec' THEN 'Babtec'
    WHEN 'av_gewatec' THEN 'Gewatec'
    WHEN 'av_provis' THEN 'Provis'
    WHEN 'qmb_consense' THEN 'Consense'
    ELSE name
END
WHERE responsibility_key IN (
    'it_ad',
    'it_mailbox',
    'it_habel',
    'it_ln',
    'it_hardware',
    'qs_babtec',
    'av_gewatec',
    'av_provis',
    'qmb_consense'
);
