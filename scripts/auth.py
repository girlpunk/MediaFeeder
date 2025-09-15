"""Configuration for MediaFeeder Clients."""

import logging
import time
from datetime import datetime, timedelta, timezone
from http import HTTPStatus
from pathlib import Path
from typing import Any
from urllib.parse import urljoin

import requests
import yaml
from yamlable import YamlAble, yaml_info

#@yaml_info(yaml_tag_ns='MediaFeederPlayerConfig')
#class Config(YamlAble):
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
    _settings: Any = None

    def __init__(self) -> None:
        """Setup."""
        self._logger = logging.getLogger("MediaFeederConfig")
        self._logger.debug("Config init")

        self.load_settings()
        self._load_metadata()

    def load_settings(self) -> None:
        """Load settings from YAML file."""
        self._logger.debug("Load settings")
        if self._settings is None:
            with Path("appsettings.yaml").open(encoding="utf-8") as f:
                self._settings = yaml.safe_load(f)

    def get_player(self, player: str) -> dict[str, Any]:
        """Get details about a player.

        Returns:
            dict[str, Any]: Player settings

        """
        self._logger.debug("Get Player")
        return self._settings["Players"][player]

    def save_player(self, player: str, settings: dict[str, Any]) -> None:
        """Save player config back to settings."""
        self._logger.debug("Save Player")
        self._settings["Players"][player] = settings
        self._save_settings()

    def _save_settings(self) -> None:
        """Save settings back to YAML file."""
        self._logger.debug("Save settings")
        with Path("appsettings.yaml").open("w", encoding="utf-8") as f:
            yaml.dump(self._settings, f)

    def get_server(self) -> str:
        """Get the address of the MediaFeeder server.

        Returns:
            Server address

        """
        self._logger.debug("Get Server")
        return self._settings["MediaFeederUrl"]

    def get_certificate_path(self) -> str | None:
        """Get the certificate of the MediaFeeder server.

        Returns:
            Path to certificate, or None if unset

        """
        self._logger.debug("Get Certificate Path")
        if "Certificate" in self._settings:
            return self._settings["Certificate"]
        return None

    def get_server_token(self) -> str:
        """Get a token to authenticate to the MediaFeeder server.

        Returns:
            API Token

        Raises:
            RequestException: On token retrieval error

        """
        self._logger.debug("Get Server Token")
        if self._settings["Auth"]["Server"]["Token"] is not None and \
            self._settings["Auth"]["Server"]["Expiry"] is not None and \
            self._settings["Auth"]["Server"]["Expiry"] - datetime.now(tz=timezone.utc) > timedelta(0):
            return self._settings["Auth"]["Server"]["Token"]

        token_endpoint = self._metadata["token_endpoint"]

        token_response = requests.post(
            token_endpoint,
            data={
                "grant_type": "client_credentials",
                "client_assertion_type": "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
                "client_assertion": self.get_token(),
                "client_id": self._settings["Auth"]["Server"]["ClientId"],
            },
            timeout=30,
        )

        if token_response.status_code == HTTPStatus.OK:
            token = token_response.json()

            self._settings["Auth"]["Server"]["Token"] = token["access_token"]
            self._settings["Auth"]["Server"]["Expiry"] = datetime.now(tz=timezone.utc) + timedelta(seconds=token["expires_in"])

            self._save_settings()
            return token["access_token"]

        self._logger.error("Error getting server token: %s", token_response.json()["error_description"])
        token_response.raise_for_status()
        raise requests.exceptions.RequestException

    def get_token(self) -> str:
        """Get an API token for the client.

        Returns:
            API Token

        """
        self._logger.debug("Get Token")
        # Check if we have a token already
        if self._settings["Auth"]["Device"]["Token"] is not None and \
            self._settings["Auth"]["Device"]["Expiry"] is not None and \
            self._settings["Auth"]["Device"]["Expiry"] - datetime.now(tz=timezone.utc) > timedelta(0):
            return self._settings["Auth"]["Device"]["Token"]

        return self._use_refresh()

    def _load_metadata(self) -> None:
        """Load OIDC Metadata."""
        self._logger.debug("Load Metadata")
        if self._metadata is None:
            authentik_url = self._settings["Auth"]["Url"]

            oidc_configuration_request = requests.get(urljoin(authentik_url, ".well-known/openid-configuration"), timeout=30)
            oidc_configuration_request.raise_for_status()
            oidc_configuration = oidc_configuration_request.json()

            self._metadata = oidc_configuration

    def _use_refresh(self) -> str:
        """Use the stored refresh token to get a new auth token.

        Returns:
            API Token

        Raises:
            RequestException: On token retrieval error

        """
        self._logger.debug("Use Refresh")
        if self._settings["Auth"]["Device"]["Refresh"] is None:
            return self._get_new_token()

        token_endpoint = self._metadata["token_endpoint"]

        client_id = self._settings["Auth"]["Device"]["ClientId"]

        refresh_response = requests.post(
            token_endpoint,
            data={
                "grant_type": "refresh_token",
                "refresh_token": self._settings["Auth"]["Device"]["Refresh"],
                "client_id": client_id,
            },
            timeout=30,
        )

        if refresh_response.status_code == HTTPStatus.OK:
            token = refresh_response.json()

            self._settings["Auth"]["Device"]["Token"] = token["access_token"]
            self._settings["Auth"]["Device"]["Expiry"] = datetime.now(tz=timezone.utc) + timedelta(seconds=token["expires_in"])
            self._settings["Auth"]["Device"]["Refresh"] = token["refresh_token"]

            self._save_settings()
            return token["access_token"]

        if refresh_response.status_code == HTTPStatus.BAD_REQUEST and refresh_response.json()["error"] == "invalid_grant":
            self._logger.error("Error refreshing auth: %s", refresh_response.json()["error_description"])
            return self._get_new_token()

        self._logger.error("Error using refresh token: %s", refresh_response.json()["error_description"])
        refresh_response.raise_for_status()
        raise requests.exceptions.RequestException

    def _get_new_token(self) -> str:
        """Get a new refresh token.

        Returns:
            API Token

        Raises:
            Timeout: If the device code flow is not completed

        """
        self._logger.debug("Get new token")
        client_id = self._settings["Auth"]["Device"]["ClientId"]

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

                self._settings["Auth"]["Device"]["Token"] = token["access_token"]
                self._settings["Auth"]["Device"]["Expiry"] = datetime.now(tz=timezone.utc) + timedelta(seconds=token["expires_in"])
                self._settings["Auth"]["Device"]["Refresh"] = token["refresh_token"]

                self._save_settings()

                return token["access_token"]
            if token_response.status_code == HTTPStatus.BAD_REQUEST and token_response.json()["error"] == "authorization_pending":
                continue

            self._logger.error("Error retrieving token: %s", token_response.json())
            token_response.raise_for_status()

        retries -= 1

        self._logger.error("Timed out retrieving token. Please try again.")
        raise requests.exceptions.Timeout
