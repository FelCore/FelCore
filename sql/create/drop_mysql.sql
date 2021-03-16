REVOKE ALL PRIVILEGES ON `fel_auth` . * FROM 'fel'@'localhost';
REVOKE ALL PRIVILEGES ON `fel_characters` . * FROM 'fel'@'localhost';
REVOKE ALL PRIVILEGES ON `fel_world` . * FROM 'fel'@'localhost';

REVOKE GRANT OPTION ON `fel_auth` . * FROM 'fel'@'localhost';
REVOKE GRANT OPTION ON `fel_characters` . * FROM 'fel'@'localhost';
REVOKE GRANT OPTION ON `fel_world` . * FROM 'fel'@'localhost';

DROP USER 'fel'@'localhost';

DROP DATABASE IF EXISTS `fel_auth`;
DROP DATABASE IF EXISTS `fel_characters`;
DROP DATABASE IF EXISTS `fel_world`;
