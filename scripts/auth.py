"""Configuration for MediaFeeder Clients."""

import logging
import time
from datetime import datetime, timedelta, timezone
from http import HTTPStatus
from typing import Any
from urllib.parse import urljoin

import requests

from config_file import ConfigFile

# @yaml_info(yaml_tag_ns='MediaFeederPlayerConfig')
# class Config(YamlAble):
#    """Network Player Configuration."""
#
#    yaml_loader = yaml.SafeLoader
#    yaml_tag = "!MediaFeederPlayerConfig"
#
#    @yaml_info(yaml_tag_ns='MediaFeederPlayerConfigAuth')
#    class AuthConfig(YamlAble):
#        """Authentication settings."""
#
#        yaml_loader = yaml.SafeLoader
#        yaml_tag = "!MediaFeederPlayerConfigAuth"
#
#        @yaml_info(yaml_tag_ns='MediaFeederPlayerConfigClient')
#        class Client(YamlAble):
#            """OIDC Client."""
#
#            yaml_loader = yaml.SafeLoader
#            yaml_tag = "!MediaFeederPlayerConfigClient"
#
#            ClientId: str
#            Token: str | None
#            Expiry: datetime | None
#            Refresh: str | None
#
#        Device: Client
#        Server: Client
#        Url: str
#
#    MediaFeederUrl: str
#    Auth: AuthConfig
#
#    Players: dict[str, Any] = {}
#
#    Certificate: str | None = None


class MediaFeederConfig:
    """Configuration for MediaFeeder clients."""

    _metadata: dict[str, str | datetime] = None
    _config = ConfigFile("appsettings.yaml")

    def __init__(self) -> None:
        """Setup."""
        self._logger = logging.getLogger("MediaFeederConfig")
        self._logger.debug("Config init")

        self._load_metadata()

    def get_player(self, player: str) -> dict[str, Any]:
        """Get details about a player.

        Returns:
            dict[str, Any]: Player settings

        """
        self._logger.debug("Get Player")
        with self._config as cfg:
            return cfg["Players"][player]

    def save_player(self, player: str, settings: dict[str, Any]) -> None:
        """Save player config back to settings."""
        self._logger.debug("Save Player")
        with self._config as cfg:
            if "Players" not in cfg:
                cfg["Players"] = {}

            cfg["Players"][player] = settings
            cfg.write()

    def get_server(self) -> str:
        """Get the address of the MediaFeeder server.

        Returns:
            Server address

        """
        self._logger.debug("Get Server")
        with self._config as cfg:
            return cfg["MediaFeederUrl"]

    def get_certificate_path(self) -> str | None:
        """Get the certificate of the MediaFeeder server.

        Returns:
            Path to certificate, or None if unset

        """
        self._logger.debug("Get Certificate Path")
        with self._config as cfg:
            if "Certificate" in cfg:
                return cfg["Certificate"]
            return None

    def get_server_token(self) -> str:
        # TODO caching goes here
        with self._config as cfg:
            return self._get_server_token(cfg)

    def _get_server_token(self, cfg) -> str:
        """Get a token to authenticate to the MediaFeeder server.

        Returns:
            API Token

        Raises:
            RequestException: On token retrieval error

        """
        self._logger.debug("Get Server Token")
        if (
            "Token" in cfg["Auth"]["Server"]
            and cfg["Auth"]["Server"]["Token"] is not None
            and cfg["Auth"]["Server"]["Expiry"] is not None
            and cfg["Auth"]["Server"]["Expiry"] - datetime.now(tz=timezone.utc) > timedelta(0)
        ):
            return cfg["Auth"]["Server"]["Token"]

        token_endpoint = self._metadata["token_endpoint"]

        token_response = requests.post(
            token_endpoint,
            data={
                "grant_type": "client_credentials",
                "client_assertion_type": "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
                "client_assertion": self.get_token(cfg),
                "client_id": cfg["Auth"]["Server"]["ClientId"],
            },
            timeout=30,
        )

        if token_response.status_code == HTTPStatus.OK:
            token = token_response.json()

            cfg["Auth"]["Server"]["Token"] = token["access_token"]
            cfg["Auth"]["Server"]["Expiry"] = datetime.now(tz=timezone.utc) + timedelta(seconds=token["expires_in"])

            cfg.write()
            return token["access_token"]

        self._logger.error("Error getting server token: %s", token_response.json()["error_description"])
        token_response.raise_for_status()
        raise requests.exceptions.RequestException

    def get_token(self, cfg) -> str:
        """Get an API token for the client.

        Returns:
            API Token

        """
        self._logger.debug("Get Token")
        # Check if we have a token already
        if (
            "Token" in cfg["Auth"]["Server"]
            and cfg["Auth"]["Device"]["Token"] is not None
            and cfg["Auth"]["Device"]["Expiry"] is not None
            and cfg["Auth"]["Device"]["Expiry"] - datetime.now(tz=timezone.utc) > timedelta(0)
        ):
            return cfg["Auth"]["Device"]["Token"]

        return self._use_refresh(cfg)

    def _load_metadata(self) -> None:
        """Load OIDC Metadata."""
        self._logger.debug("Load Metadata")
        if self._metadata is None:
            with self._config as cfg:
                authentik_url = cfg["Auth"]["Url"]

            oidc_configuration_request = requests.get(urljoin(authentik_url, ".well-known/openid-configuration"), timeout=30)
            oidc_configuration_request.raise_for_status()
            oidc_configuration = oidc_configuration_request.json()

            self._metadata = oidc_configuration

    def _use_refresh(self, cfg) -> str:
        """Use the stored refresh token to get a new auth token.

        Returns:
            API Token

        Raises:
            RequestException: On token retrieval error

        """
        self._logger.debug("Use Refresh")
        if "Refresh" not in cfg["Auth"]["Device"] or cfg["Auth"]["Device"]["Refresh"] is None:
            return self._get_new_token(cfg)

        token_endpoint = self._metadata["token_endpoint"]

        client_id = cfg["Auth"]["Device"]["ClientId"]

        refresh_response = requests.post(
            token_endpoint,
            data={
                "grant_type": "refresh_token",
                "refresh_token": cfg["Auth"]["Device"]["Refresh"],
                "client_id": client_id,
            },
            timeout=30,
        )

        if refresh_response.status_code == HTTPStatus.OK:
            token = refresh_response.json()

            cfg["Auth"]["Device"]["Token"] = token["access_token"]
            cfg["Auth"]["Device"]["Expiry"] = datetime.now(tz=timezone.utc) + timedelta(seconds=token["expires_in"])
            cfg["Auth"]["Device"]["Refresh"] = token["refresh_token"]

            cfg.write()
            return token["access_token"]

        if refresh_response.status_code == HTTPStatus.BAD_REQUEST and refresh_response.json()["error"] == "invalid_grant":
            self._logger.error("Error refreshing auth: %s", refresh_response.json()["error_description"])
            return self._get_new_token(cfg)

        self._logger.error("Error using refresh token: %s", refresh_response.json()["error_description"])
        refresh_response.raise_for_status()
        raise requests.exceptions.RequestException

    def _get_new_token(self, cfg) -> str:
        """Get a new refresh token.

        Returns:
            API Token

        Raises:
            Timeout: If the device code flow is not completed

        """
        self._logger.debug("Get new token")
        client_id = cfg["Auth"]["Device"]["ClientId"]

        device_endpoint = self._metadata["device_authorization_endpoint"]
        token_endpoint = self._metadata["token_endpoint"]

        device_auth_response = requests.post(
            device_endpoint,
            data={
                "client_id": client_id,
                "scope": "openid offline_access",
            },
            timeout=30,
        )
        device_auth_response.raise_for_status()
        darj = device_auth_response.json()
        device_code = darj["device_code"]

        if "verification_uri_complete" in darj:
            self._logger.info("Please visit %s to authenticate", darj["verification_uri_complete"])
        else:
            self._logger.info("Please visit %s and paste the code %s to authenticate.", darj["user_code"], darj["verification_uri"])

        retries = 60

        while True:
            time.sleep(int(darj["interval"]))

            token_response = requests.post(
                token_endpoint,
                data={
                    "client_id": client_id,
                    "device_code": device_code,
                    "grant_type": "urn:ietf:params:oauth:grant-type:device_code",
                },
                timeout=30,
            )

            if token_response.status_code == HTTPStatus.OK:
                # Successfully retrieved the token
                token = token_response.json()

                cfg["Auth"]["Device"]["Token"] = token["access_token"]
                cfg["Auth"]["Device"]["Expiry"] = datetime.now(tz=timezone.utc) + timedelta(seconds=token["expires_in"])
                cfg["Auth"]["Device"]["Refresh"] = token["refresh_token"]

                cfg.write()

                return token["access_token"]
            if token_response.status_code == HTTPStatus.BAD_REQUEST and token_response.json()["error"] == "authorization_pending":
                continue

            self._logger.error("Error retrieving token: %s", token_response.json())
            token_response.raise_for_status()

        retries -= 1

        self._logger.error("Timed out retrieving token. Please try again.")
        raise requests.exceptions.Timeout


# vim: tw=0 ts=4 sw=4
