# Open Blocks - 3D Modelling for Everyone

![Current Version](https://img.shields.io/github/v/release/icosa-foundation/open-blocks)
![Prerelease Version](https://img.shields.io/github/v/release/icosa-foundation/open-blocks?include_prereleases&label=prerelease)
[![Support us on Open Collective!](https://img.shields.io/opencollective/all/icosa?logo=open-collective&label=Support%20us%20on%20Open%20Collective%21)](https://opencollective.com/icosa)
[![X](https://img.shields.io/badge/follow-%40openblocksapp-blue.svg?style=flat&logo=x)](https://x.com/openblocksapp)
[![Discord](https://discordapp.com/api/guilds/783806589991780412/embed.png?style=shield)](https://discord.gg/W7NCEYnEfy)


[![Open Blocks Banner](open-blocks.png)](https://openblocks.app)

Open Blocks is a free fork of Blocks by Google, An app designed to make creating 3D models fun, easy, and accessible. We are in the process of making large number of changes. Please check our progress and our roadmap on our [docs site](https://docs.openblocks.app).

We hope to maintain and improve upon Blocks as a community-led project, free forever!

As the original repo is archived we cannot submit PRs, so feel free to submit them here!

[User Guide](https://docs.openblocks.app/)  
[Please join the Icosa Discord and get involved!](https://discord.com/invite/W7NCEYnEfy)  
[Support us on Open Collective](https://opencollective.com/icosa)

## Downloads
### Stores
(Coming Soon)

### GitHub
- [Formal GitHub Releases](https://github.com/icosa-foundation/open-blocks/releases/latest)

Note that despite a Windows build, it does work on Linux. For example one can add as a non-Steam game to their library, force compatibility with Proton experimental and run Open Blocks as-is, without any modification.

## Acknowledgements
* Thank you to the original developers for your amazing work and for finding a way to open source the app!

## Important note from the original Blocks README

The Blocks trademark and logo (“Blocks Trademarks”) are trademarks of
Google, and are treated separately from the copyright or patent license grants
contained in the Apache-licensed Blocks repositories on GitHub. Any use of
the Blocks Trademarks other than those permitted in these guidelines must be
approved in advance.

For more information, read the
[Blocks Brand Guidelines](BRAND_GUIDELINES.md).

---

# Building the application

Get the Open Blocks open-source application running on your own devices.

### Prerequisites

*   [Unity 2019.4.25f1](unityhub://2019.4.25f1/01a0494af254)

### Changing the application name

_Blocks_ is a Google trademark. If you intend to publish a cloned version of
the application, you are required to choose a different name to distinguish it
from the official version. Before building the application, go into `App.cs` and
the Player settings to change the company and application names to your own.

Please see the [Blocks Brand Guidelines](BRAND_GUIDELINES.md) for more details.

## Systems that were replaced or removed when open-sourcing Blocks

Some systems in Blocks were removed or replaced with alternatives due to
open-source licensing issues. These are:

* AnimatedGifEncoder32
* LZWEncoder

## Known issues

OculusVR mode and reference image insertion are not currently functional in this
branch.

## Google service API support

Legacy code is included to connect to Google APIs for People and Drive
integrations. This is not critical to the Blocks experience, but is left
as a convenience for any forks that wish to make use of it with a new backend.

You must register new projects and obtain new keys and credentials from the
Google Cloud Console to make use of these features.
